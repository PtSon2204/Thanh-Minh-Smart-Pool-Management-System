using System.Net.Mail;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.StaffOnboarding;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    private StaffOnboardingServices Onboarding => onboardingServices ?? throw new StaffOnboardingException("ONBOARDING_UNAVAILABLE", 503);
    private DateTime OnboardingNow => timeProvider.GetUtcNow().UtcDateTime;

    private Task LockOnboardingAsync(CancellationToken cancellationToken) => context.Database.ExecuteSqlRawAsync(
        "SELECT pg_advisory_xact_lock(704318229012)", cancellationToken);

    public async Task<OnboardingWorkspace> ExecuteOnboardingAsync(OnboardingCommand request, CancellationToken cancellationToken)
    {
        if (request.Action == OnboardingAction.Upload) return await UploadOnboardingDocumentAsync(request, cancellationToken);
        string? tokenToSend = null;
        OnboardingWorkspace result;
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            if (request.Action is not (OnboardingAction.Inspect or OnboardingAction.Detail or OnboardingAction.Review or
                OnboardingAction.Own or OnboardingAction.EmployeeDetail))
                await LockOnboardingAsync(cancellationToken);
            if (request.Action is OnboardingAction.Create or OnboardingAction.Detail or OnboardingAction.Resend or
                OnboardingAction.Cancel or OnboardingAction.Review or OnboardingAction.Approve or
                OnboardingAction.RequestChanges or OnboardingAction.Reject or OnboardingAction.OpenCorrection or OnboardingAction.EmployeeDetail)
                await RequireOnboardingAdminAsync(request.ActorId, cancellationToken);

            StaffInvitation? invitation = null;
            StaffOnboardingRevision? revision = null;
            Guid? employeeId = null;
            var code = "OK";
            if (request.Action == OnboardingAction.Create)
            {
                var emailAddress = request.Email?.Trim().ToLowerInvariant() ?? "";
                if (emailAddress.Length > 150 || !MailAddress.TryCreate(emailAddress, out var parsed) || parsed.Address != emailAddress ||
                    request.TargetRole is not ("SALES" or "LIFEGUARD" or "COACH") || request.JoinDate is null)
                    throw new StaffOnboardingException("INVALID_INVITATION", 400);
                StaffOnboardingValidation.ValidateJoinDate(request.JoinDate, timeProvider);
                await CheckAccountIdentityAsync(emailAddress, cancellationToken);
                if (await context.StaffInvitations.AnyAsync(item => item.Email == emailAddress &&
                    (item.Status == "PENDING" || item.Status == "EXPIRED"), cancellationToken))
                    throw new StaffOnboardingException("INVITATION_EXISTS");
                invitation = new StaffInvitation { Email = emailAddress, TargetRole = request.TargetRole,
                    JoinDate = request.JoinDate.Value, CreatedBy = request.ActorId, CreatedAtUtc = OnboardingNow };
                tokenToSend = RotateInvitation(invitation);
                Onboarding.InvitationLink(invitation.Id, tokenToSend);
                var profile = StaffOnboardingValidation.Normalize(new StaffLegalProfile { FullName = request.FullName ?? "", Phone = request.Phone ?? "" });
                await new StaffInvitationProfileValidator().ValidateAndThrowAsync(profile, cancellationToken);
                revision = new StaffOnboardingRevision { InvitationId = invitation.Id, CreatedAtUtc = OnboardingNow,
                    ProtectedProfile = Onboarding.ProtectProfile(profile) };
                context.StaffInvitations.Add(invitation);
                context.StaffOnboardingRevisions.Add(revision);
            }
            else
            {
                (invitation, revision, employeeId) = await ResolveOnboardingAsync(request, cancellationToken);
                switch (request.Action)
                {
                    case OnboardingAction.Inspect:
                    case OnboardingAction.Detail:
                    case OnboardingAction.Review:
                    case OnboardingAction.Own:
                    case OnboardingAction.EmployeeDetail:
                        break;
                    case OnboardingAction.Resend:
                        RequirePending(invitation!);
                        StaffOnboardingValidation.CheckVersion(invitation!.Version, request.ExpectedVersion);
                        if (revision?.State is not ("DRAFT" or "CHANGES_REQUESTED"))
                            throw new StaffOnboardingException("RESEND_NOT_ALLOWED");
                        tokenToSend = RotateInvitation(invitation);
                        Onboarding.InvitationLink(invitation.Id, tokenToSend);
                        invitation.Version++;
                        break;
                    case OnboardingAction.Cancel:
                        RequirePending(invitation!);
                        StaffOnboardingValidation.CheckVersion(invitation!.Version, request.ExpectedVersion);
                        invitation.Status = "CANCELLED";
                        invitation.TokenDigest = "";
                        invitation.ProtectedPasswordHash = null;
                        invitation.Version++;
                        var revisions = await context.StaffOnboardingRevisions.Where(item => item.InvitationId == invitation.Id).ToListAsync(cancellationToken);
                        foreach (var item in revisions.Where(item => item.State != "APPROVED"))
                        {
                            item.State = "REJECTED";
                            item.ReviewerId = request.ActorId;
                            item.ReviewedAtUtc = OnboardingNow;
                            item.Version++;
                            await QueueDocumentsAsync(item.Id, "CANCELLED", cancellationToken);
                        }
                        await ReleaseIdentityClaimsAsync(invitation.Id, null, cancellationToken);
                        break;
                    case OnboardingAction.Draft:
                        RequireEditable(revision, request.ExpectedVersion);
                        var profile = StaffOnboardingValidation.Normalize(request.Profile ?? throw new StaffOnboardingException("PROFILE_REQUIRED", 400));
                        await new StaffLegalProfileValidator(false, timeProvider).ValidateAndThrowAsync(profile, cancellationToken);
                        revision!.ProtectedProfile = Onboarding.ProtectProfile(profile);
                        revision.State = "DRAFT";
                        revision.Version++;
                        break;
                    case OnboardingAction.Submit:
                        await SubmitOnboardingAsync(request, invitation, revision, employeeId, cancellationToken);
                        break;
                    case OnboardingAction.Approve:
                    case OnboardingAction.RequestChanges:
                    case OnboardingAction.Reject:
                        code = await ReviewOnboardingAsync(request, invitation, revision!, employeeId, cancellationToken);
                        break;
                    case OnboardingAction.RequestCorrection:
                        await RequestCorrectionAsync(request, employeeId!.Value, cancellationToken);
                        break;
                    case OnboardingAction.OpenCorrection:
                        revision = await OpenCorrectionAsync(request, employeeId!.Value, cancellationToken);
                        break;
                    default:
                        throw new StaffOnboardingException("INVALID_ACTION", 400);
                }
            }
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { throw new StaffOnboardingException("STALE_VERSION"); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
            { throw new StaffOnboardingException("IDENTITY_CONFLICT"); }
            result = await WorkspaceAsync(code, invitation, revision, employeeId ?? invitation?.CompletedUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (tokenToSend is not null)
        {
            await DeliverInvitationAsync(result.Invitation!, tokenToSend, cancellationToken);
            var refreshed = await context.StaffInvitations.AsNoTracking().SingleAsync(item => item.Id == result.Invitation!.Id, cancellationToken);
            result = result with { Invitation = InvitationView(refreshed) };
        }
        if (request.Action is OnboardingAction.Approve or OnboardingAction.Reject or OnboardingAction.Cancel)
        {
            try { await Onboarding.Cleanup.RunBatchAsync(cancellationToken); }
            catch (Exception) { result = result with { Code = result.Code + "_CLEANUP_PENDING" }; }
        }
        return result;
    }

    private async Task<(StaffInvitation? Invitation, StaffOnboardingRevision? Revision, Guid? EmployeeId)> ResolveOnboardingAsync(
        OnboardingCommand request, CancellationToken cancellationToken)
    {
        if (request.Action is OnboardingAction.Inspect ||
            (request.Action is OnboardingAction.Draft or OnboardingAction.Submit or OnboardingAction.Upload && request.InvitationId.HasValue))
        {
            var invitation = await ValidateInvitationTokenAsync(request.InvitationId, request.Token, cancellationToken);
            var revision = await context.StaffOnboardingRevisions.SingleAsync(item => item.InvitationId == invitation.Id, cancellationToken);
            return (invitation, revision, null);
        }
        if (request.Action is OnboardingAction.Detail or OnboardingAction.Resend or OnboardingAction.Cancel)
        {
            var invitation = await context.StaffInvitations.SingleOrDefaultAsync(item => item.Id == request.InvitationId, cancellationToken)
                ?? throw new StaffOnboardingException("INVITATION_NOT_FOUND", 404);
            return (invitation, await context.StaffOnboardingRevisions.SingleAsync(item => item.InvitationId == invitation.Id, cancellationToken), invitation.CompletedUserId);
        }
        if (request.Action is OnboardingAction.Review or OnboardingAction.Approve or OnboardingAction.RequestChanges or OnboardingAction.Reject)
        {
            var revision = await context.StaffOnboardingRevisions.SingleOrDefaultAsync(item => item.Id == request.RevisionId, cancellationToken)
                ?? throw new StaffOnboardingException("REVISION_NOT_FOUND", 404);
            var invitation = revision.InvitationId.HasValue
                ? await context.StaffInvitations.SingleAsync(item => item.Id == revision.InvitationId, cancellationToken) : null;
            return (invitation, revision, revision.EmployeeId ?? invitation?.CompletedUserId);
        }
        var employeeId = request.Action is OnboardingAction.OpenCorrection or OnboardingAction.EmployeeDetail ? request.EmployeeId : request.ActorId;
        if (!employeeId.HasValue || !await context.Employees.AnyAsync(item => item.UserId == employeeId &&
            item.User.IsDeleted != true && item.User.Status == "Active", cancellationToken))
            throw new StaffOnboardingException("EMPLOYEE_REQUIRED", 403);
        var latest = await EmployeeRevisions(employeeId.Value).OrderByDescending(item => item.RevisionNumber).FirstOrDefaultAsync(cancellationToken);
        return (null, latest, employeeId);
    }

    private async Task RequireOnboardingAdminAsync(Guid actorId, CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(item => item.Id == actorId && item.IsDeleted != true &&
            item.Status != null && EF.Functions.ILike(item.Status, "ACTIVE") && item.Role != null &&
            item.Role.IsDeleted != true && EF.Functions.ILike(item.Role.Name, "ADMIN"), cancellationToken))
            throw new StaffOnboardingException("ADMIN_REQUIRED", 403);
    }

    private async Task CheckAccountIdentityAsync(string emailAddress, CancellationToken cancellationToken)
    {
        var existing = await context.Users.IgnoreQueryFilters().Where(item => item.Email != null && item.Email.Trim().ToLower() == emailAddress)
            .Select(item => new { HasEmployee = item.Employee != null }).ToListAsync(cancellationToken);
        if (existing.Count != 0) throw new StaffOnboardingException(existing.Any(item => item.HasEmployee) ? "EXISTING_STAFF" : "ACCOUNT_ALREADY_EXISTS");
    }

    private async Task CheckPhoneAsync(string phone, Guid? employeeId, CancellationToken cancellationToken)
    {
        var international = "+84" + phone[1..];
        if (await context.Users.IgnoreQueryFilters().AnyAsync(item => item.Id != employeeId && item.Phone != null &&
            (item.Phone.Trim() == phone || item.Phone.Trim() == international), cancellationToken))
            throw new StaffOnboardingException("PHONE_ALREADY_EXISTS");
    }

    private static void RequirePending(StaffInvitation invitation)
    {
        if (invitation.Status is not ("PENDING" or "EXPIRED")) throw new StaffOnboardingException("INVITATION_CLOSED");
    }

    private static void RequireEditable(StaffOnboardingRevision? revision, long? expectedVersion)
    {
        if (revision is null || revision.State is not ("DRAFT" or "CHANGES_REQUESTED")) throw new StaffOnboardingException("REVISION_LOCKED");
        StaffOnboardingValidation.CheckVersion(revision.Version, expectedVersion);
    }

    private async Task<StaffInvitation> ValidateInvitationTokenAsync(Guid? invitationId, string? token, CancellationToken cancellationToken)
    {
        if (invitationId is null || token is null || token.Length != 64) throw new StaffOnboardingException("INVALID_INVITATION_TOKEN", 403);
        var invitation = await context.StaffInvitations.SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);
        if (invitation is null || invitation.Status != "PENDING" || invitation.ExpiresAtUtc <= OnboardingNow ||
            !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(invitation.TokenDigest),
                System.Text.Encoding.ASCII.GetBytes(StaffOnboardingServices.TokenDigest(invitation.Id, token))))
            throw new StaffOnboardingException("INVALID_OR_EXPIRED_INVITATION", 403);
        return invitation;
    }

    private string RotateInvitation(StaffInvitation invitation)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        invitation.TokenDigest = StaffOnboardingServices.TokenDigest(invitation.Id, token);
        invitation.ExpiresAtUtc = OnboardingNow.AddHours(24);
        invitation.Status = "PENDING";
        invitation.DeliveryStatus = "PENDING";
        invitation.DeliveryErrorCode = null;
        return token;
    }

    private async Task DeliverInvitationAsync(StaffInvitationView invitation, string token, CancellationToken cancellationToken)
    {
        var status = "SENT";
        string? error = null;
        try
        {
            var link = Onboarding.InvitationLink(invitation.Id, token);
            await Onboarding.Email.SendAsync(invitation.Email, StaffInvitationEmail.Subject,
                StaffInvitationEmail.Body(link), cancellationToken);
        }
        catch (Exception) { status = "FAILED"; error = "DELIVERY_FAILED_OR_UNCERTAIN"; }
        var digest = StaffOnboardingServices.TokenDigest(invitation.Id, token);
        await context.StaffInvitations.Where(item => item.Id == invitation.Id && item.TokenDigest == digest && item.Status == "PENDING")
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.DeliveryStatus, status)
                .SetProperty(item => item.DeliveryErrorCode, error), CancellationToken.None);
    }

    private IQueryable<StaffOnboardingRevision> EmployeeRevisions(Guid employeeId) => context.StaffOnboardingRevisions.Where(item =>
        item.EmployeeId == employeeId || context.StaffInvitations.Any(invitation => invitation.Id == item.InvitationId && invitation.CompletedUserId == employeeId));
}

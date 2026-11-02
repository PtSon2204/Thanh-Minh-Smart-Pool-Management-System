using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    private async Task SubmitOnboardingAsync(OnboardingCommand request, StaffInvitation? invitation,
        StaffOnboardingRevision? revision, Guid? employeeId, CancellationToken cancellationToken)
    {
        RequireEditable(revision, request.ExpectedVersion);
        var profile = StaffOnboardingValidation.Normalize(request.Profile ?? Onboarding.Profile(revision!.ProtectedProfile));
        await new StaffLegalProfileValidator(true, timeProvider).ValidateAndThrowAsync(profile, cancellationToken);
        if (invitation is not null)
        {
            await CheckAccountIdentityAsync(invitation.Email, cancellationToken);
            StaffOnboardingValidation.ValidatePassword(request.Password, request.PasswordConfirmation);
            invitation.ProtectedPasswordHash = Onboarding.Protect(Onboarding.Passwords.HashPassword(new User(), request.Password!), "staff-password-v1");
        }
        await CheckPhoneAsync(profile.Phone, employeeId, cancellationToken);
        var ownerId = employeeId ?? invitation!.Id;
        var digest = Onboarding.IdentityDigest(profile.CccdNumber);
        var claim = await context.StaffIdentityClaims.SingleOrDefaultAsync(item => item.Digest == digest, cancellationToken);
        if (claim is not null && claim.OwnerId != ownerId) throw new StaffOnboardingException("IDENTITY_CONFLICT");
        if (claim is null) context.StaffIdentityClaims.Add(new StaffIdentityClaim { Digest = digest, OwnerId = ownerId });
        revision!.ProtectedProfile = Onboarding.ProtectProfile(profile);
        revision.CccdDigest = digest;
        revision.State = "SUBMITTED";
        revision.SubmittedAtUtc = OnboardingNow;
        revision.Version++;
    }

    private async Task<string> ReviewOnboardingAsync(OnboardingCommand request, StaffInvitation? invitation,
        StaffOnboardingRevision revision, Guid? employeeId, CancellationToken cancellationToken)
    {
        if (request.Action == OnboardingAction.Approve && revision.State == "APPROVED") return "ALREADY_APPROVED";
        StaffOnboardingValidation.CheckVersion(revision.Version, request.ExpectedVersion);
        if (revision.State != "SUBMITTED") throw new StaffOnboardingException("REVISION_NOT_SUBMITTED");
        if (invitation is not null) RequirePending(invitation);
        if (request.Action != OnboardingAction.Approve && (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000))
            throw new StaffOnboardingException("REASON_REQUIRED", 400);
        revision.ReviewerId = request.ActorId;
        revision.ReviewedAtUtc = OnboardingNow;
        revision.Version++;
        if (request.Action == OnboardingAction.RequestChanges)
        {
            revision.State = "CHANGES_REQUESTED";
            revision.CorrectionReason = request.Reason!.Trim();
            return "CHANGES_REQUESTED";
        }
        if (request.Action == OnboardingAction.Reject)
        {
            revision.State = "REJECTED";
            revision.CorrectionReason = request.Reason!.Trim();
            if (invitation is not null) { invitation.ProtectedPasswordHash = null; invitation.TokenDigest = ""; invitation.Status = "CANCELLED"; invitation.Version++; }
            await QueueDocumentsAsync(revision.Id, "REJECTED", cancellationToken);
            await ReleaseIdentityClaimsAsync(invitation?.Id, employeeId, cancellationToken);
            await CloseCorrectionAsync(revision.Id, cancellationToken);
            return "REJECTED";
        }
        if (!request.ReviewConfirmed) throw new StaffOnboardingException("REVIEW_CONFIRMATION_REQUIRED", 400);
        var profile = Onboarding.Profile(revision.ProtectedProfile);
        await CheckPhoneAsync(profile.Phone, employeeId, cancellationToken);
        if (invitation is not null)
        {
            await context.Database.ExecuteSqlRawAsync("LOCK TABLE smart_pool.users IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
            await CheckAccountIdentityAsync(invitation.Email, cancellationToken);
            if (invitation.ProtectedPasswordHash is null) throw new StaffOnboardingException("PASSWORD_REQUIRED", 400);
            var role = await context.Roles.SingleOrDefaultAsync(item => EF.Functions.ILike(item.Name, invitation.TargetRole) && item.IsDeleted != true, cancellationToken)
                ?? throw new StaffOnboardingException("ROLE_UNAVAILABLE");
            var user = new User { Id = Guid.NewGuid(), Email = invitation.Email, Username = $"staff-{Guid.NewGuid():N}",
                Phone = profile.Phone, RoleId = role.Id, Status = "Active", IsDeleted = false,
                PasswordHash = Onboarding.Unprotect(invitation.ProtectedPasswordHash, "staff-password-v1"), CreatedAt = OnboardingNow, UpdatedAt = OnboardingNow };
            context.Users.Add(user);
            var avatar = await context.StaffOnboardingDocuments.SingleOrDefaultAsync(item => item.RevisionId == revision.Id && item.Purpose == "AVATAR" && item.DeletionState == "AVAILABLE", cancellationToken);
            context.UserProfiles.Add(new UserProfile { UserId = user.Id, FullName = profile.FullName, DateOfBirth = profile.DateOfBirth,
                AvatarUrl = avatar is null ? null : Onboarding.Storage.AvatarUrl(avatar), UpdatedAt = OnboardingNow });
            context.Employees.Add(new Employee { UserId = user.Id, JoinDate = invitation.JoinDate, Status = "Working", CreatedAt = OnboardingNow, UpdatedAt = OnboardingNow });
            invitation.CompletedUserId = user.Id;
            invitation.ProtectedPasswordHash = null;
            invitation.Status = "COMPLETED";
            invitation.Version++;
            var claims = await context.StaffIdentityClaims.Where(item => item.OwnerId == invitation.Id).ToListAsync(cancellationToken);
            foreach (var claim in claims) claim.OwnerId = user.Id;
            employeeId = user.Id;
        }
        revision.State = "APPROVED";
        revision.DocumentsProvided = await context.StaffOnboardingDocuments.AnyAsync(item => item.RevisionId == revision.Id && item.Purpose != "AVATAR", cancellationToken);
        await QueueDocumentsAsync(revision.Id, "APPROVED", cancellationToken);
        await CloseCorrectionAsync(revision.Id, cancellationToken);
        // Old identity reservations remain until approval, then only approved history retains them.
        var approvedDigests = await EmployeeRevisions(employeeId!.Value).Where(item => item.State == "APPROVED" && item.CccdDigest != null)
            .Select(item => item.CccdDigest!).ToListAsync(cancellationToken);
        approvedDigests.Add(revision.CccdDigest!);
        var unused = await context.StaffIdentityClaims.Where(item => item.OwnerId == employeeId && !approvedDigests.Contains(item.Digest)).ToListAsync(cancellationToken);
        context.StaffIdentityClaims.RemoveRange(unused);
        return "APPROVED";
    }

    private async Task RequestCorrectionAsync(OnboardingCommand request, Guid employeeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000) throw new StaffOnboardingException("REASON_REQUIRED", 400);
        if (await context.StaffCorrectionRequests.AnyAsync(item => item.EmployeeId == employeeId && item.State != "CLOSED", cancellationToken))
            throw new StaffOnboardingException("CORRECTION_ALREADY_REQUESTED");
        context.StaffCorrectionRequests.Add(new StaffCorrectionRequest { EmployeeId = employeeId, Reason = request.Reason.Trim(), CreatedAtUtc = OnboardingNow });
    }

    private async Task<StaffOnboardingRevision> OpenCorrectionAsync(OnboardingCommand request, Guid employeeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000) throw new StaffOnboardingException("REASON_REQUIRED", 400);
        var correction = await context.StaffCorrectionRequests.SingleOrDefaultAsync(item => item.EmployeeId == employeeId && item.State == "REQUESTED", cancellationToken)
            ?? throw new StaffOnboardingException("CORRECTION_NOT_REQUESTED");
        StaffOnboardingValidation.CheckVersion(correction.Version, request.ExpectedVersion);
        var latest = await EmployeeRevisions(employeeId).OrderByDescending(item => item.RevisionNumber).FirstOrDefaultAsync(cancellationToken);
        var profile = latest is null ? new StaffLegalProfile() : Onboarding.Profile(latest.ProtectedProfile);
        var revision = new StaffOnboardingRevision { EmployeeId = employeeId, RevisionNumber = (latest?.RevisionNumber ?? 0) + 1,
            ProtectedProfile = Onboarding.ProtectProfile(profile), CorrectionReason = request.Reason.Trim(), CreatedAtUtc = OnboardingNow };
        context.StaffOnboardingRevisions.Add(revision);
        correction.State = "OPENED";
        correction.RevisionId = revision.Id;
        correction.Version++;
        return revision;
    }

    private async Task CloseCorrectionAsync(Guid revisionId, CancellationToken cancellationToken)
    {
        var correction = await context.StaffCorrectionRequests.SingleOrDefaultAsync(item => item.RevisionId == revisionId, cancellationToken);
        if (correction is not null) { correction.State = "CLOSED"; correction.Version++; }
    }

    private async Task ReleaseIdentityClaimsAsync(Guid? invitationId, Guid? employeeId, CancellationToken cancellationToken)
    {
        var owner = employeeId ?? invitationId;
        var keep = employeeId.HasValue ? await EmployeeRevisions(employeeId.Value).Where(item => item.State == "APPROVED" && item.CccdDigest != null)
            .Select(item => item.CccdDigest!).ToListAsync(cancellationToken) : [];
        context.StaffIdentityClaims.RemoveRange(await context.StaffIdentityClaims.Where(item => item.OwnerId == owner && !keep.Contains(item.Digest)).ToListAsync(cancellationToken));
    }

    private static StaffInvitationView InvitationView(StaffInvitation invitation) => new(invitation.Id, invitation.Email, invitation.TargetRole,
        invitation.JoinDate, invitation.Status, invitation.DeliveryStatus, invitation.DeliveryErrorCode, invitation.ExpiresAtUtc,
        invitation.CreatedAtUtc, invitation.CompletedUserId, invitation.Version);

    private async Task<StaffRevisionView?> RevisionViewAsync(StaffOnboardingRevision? revision, CancellationToken cancellationToken)
    {
        if (revision is null) return null;
        var documents = await context.StaffOnboardingDocuments.AsNoTracking().Where(item => item.RevisionId == revision.Id).OrderBy(item => item.UploadedAtUtc).ToListAsync(cancellationToken);
        return new(revision.Id, revision.RevisionNumber, revision.State, revision.Version, Onboarding.Profile(revision.ProtectedProfile),
            documents.Select(item => new StaffDocumentView(item.Id, item.Purpose,
                item.DeletionState == "AVAILABLE" && item.DeleteAfterUtc <= OnboardingNow ? "EXPIRED" : item.DeletionState == "PENDING" ? "PENDING_DELETION" : item.DeletionState,
                item.ContentType, item.UploadedAtUtc, item.DeleteAfterUtc, item.DeletedAtUtc, item.LastErrorCode)).ToList(),
            revision.SubmittedAtUtc, revision.ReviewedAtUtc, revision.ReviewerId, revision.CorrectionReason, revision.DocumentsProvided);
    }

    private async Task<OnboardingWorkspace> WorkspaceAsync(string code, StaffInvitation? invitation, StaffOnboardingRevision? revision,
        Guid? employeeId, CancellationToken cancellationToken)
    {
        var approved = employeeId.HasValue ? await EmployeeRevisions(employeeId.Value).Where(item => item.State == "APPROVED")
            .OrderByDescending(item => item.RevisionNumber).FirstOrDefaultAsync(cancellationToken) : null;
        var correction = employeeId.HasValue ? await context.StaffCorrectionRequests.Where(item => item.EmployeeId == employeeId && item.State != "CLOSED")
            .OrderByDescending(item => item.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken) : null;
        return new(code, invitation is null ? null : InvitationView(invitation), await RevisionViewAsync(revision, cancellationToken),
            await RevisionViewAsync(approved, cancellationToken), correction is null ? null : new(correction.Id, correction.EmployeeId,
                correction.Reason, correction.State, correction.CreatedAtUtc, correction.RevisionId, correction.Version));
    }

    public async Task<IReadOnlyList<StaffInvitationListItem>> ListInvitationsAsync(ListStaffInvitationsQuery request, CancellationToken cancellationToken)
    {
        await RequireOnboardingAdminAsync(request.ActorId, cancellationToken);
        var invitations = await context.StaffInvitations.AsNoTracking().OrderByDescending(item => item.CreatedAtUtc)
            .Skip((Math.Max(request.Page, 1) - 1) * Math.Clamp(request.PageSize, 1, 100)).Take(Math.Clamp(request.PageSize, 1, 100)).ToListAsync(cancellationToken);
        var result = new List<StaffInvitationListItem>();
        foreach (var invitation in invitations)
        {
            var revision = await context.StaffOnboardingRevisions.AsNoTracking().SingleAsync(item => item.InvitationId == invitation.Id, cancellationToken);
            result.Add(new(InvitationView(invitation), revision.Id, Onboarding.Profile(revision.ProtectedProfile).FullName, revision.State, revision.SubmittedAtUtc));
        }
        return result;
    }
}

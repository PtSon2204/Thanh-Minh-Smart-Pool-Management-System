using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
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
        var holder = await context.StaffOnboardingRevisions.Where(item => item.ReservedCccdDigest == digest)
            .Select(item => new
            {
                Revision = item,
                OwnerId = item.EmployeeId ?? context.StaffInvitations.Where(invitation => invitation.Id == item.InvitationId)
                    .Select(invitation => invitation.CompletedUserId ?? invitation.Id).Single()
            }).SingleOrDefaultAsync(cancellationToken);
        if (holder is not null && holder.OwnerId != ownerId) throw new StaffOnboardingException("IDENTITY_CONFLICT");
        revision!.ReservedCccdDigest = holder is null || holder.Revision.Id == revision.Id ? digest : null;
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
            await ReleaseIdentityReservationsAsync(invitation?.Id, employeeId, cancellationToken);
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
            var role = await context.Roles.Where(item => EF.Functions.ILike(item.Name, invitation.TargetRole) && item.IsDeleted != true)
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken)
                ?? throw new StaffOnboardingException("ROLE_UNAVAILABLE");
            var user = new User { Id = Guid.NewGuid(), Email = invitation.Email, Username = $"staff-{Guid.NewGuid():N}",
                Phone = profile.Phone, RoleId = role.Id, Status = "Active", IsDeleted = false,
                PasswordHash = Onboarding.Unprotect(invitation.ProtectedPasswordHash, "staff-password-v1"), CreatedAt = OnboardingNow, UpdatedAt = OnboardingNow };
            context.Users.Add(user);
            var avatar = await context.StaffOnboardingDocuments.SingleOrDefaultAsync(item => item.RevisionId == revision.Id && item.Purpose == "AVATAR" && item.DeletionState == "AVAILABLE", cancellationToken);
            context.UserProfiles.Add(new UserProfile { UserId = user.Id, FullName = profile.FullName, DateOfBirth = profile.DateOfBirth,
                AvatarUrl = avatar is null ? null : Onboarding.Storage.AvatarUrl(avatar), UpdatedAt = OnboardingNow });
            context.Employees.Add(new Employee { UserId = user.Id, User = user, JoinDate = invitation.JoinDate, Status = "Working", CreatedAt = OnboardingNow, UpdatedAt = OnboardingNow });
            invitation.CompletedUserId = user.Id;
            invitation.ProtectedPasswordHash = null;
            invitation.Status = "COMPLETED";
            invitation.Version++;
            employeeId = user.Id;
        }
        revision.State = "APPROVED";
        revision.DocumentsProvided = await context.StaffOnboardingDocuments.AnyAsync(item => item.RevisionId == revision.Id && item.Purpose != "AVATAR", cancellationToken);
        await QueueDocumentsAsync(revision.Id, "APPROVED", cancellationToken);
        await CloseCorrectionAsync(revision.Id, cancellationToken);
        return "APPROVED";
    }

    private async Task RequestCorrectionAsync(OnboardingCommand request, Guid employeeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000) throw new StaffOnboardingException("REASON_REQUIRED", 400);
        var employee = await context.Employees.SingleOrDefaultAsync(item => item.UserId == employeeId, cancellationToken)
            ?? throw new StaffOnboardingException("EMPLOYEE_NOT_FOUND", 404);
        if (employee.CorrectionRequestId.HasValue)
            throw new StaffOnboardingException("CORRECTION_ALREADY_REQUESTED");
        employee.CorrectionRequestId = Guid.NewGuid();
        employee.CorrectionReason = request.Reason.Trim();
        employee.CorrectionRequestedAtUtc = OnboardingNow;
        employee.CorrectionRevisionId = null;
        employee.CorrectionVersion++;
    }

    private async Task<StaffOnboardingRevision> OpenCorrectionAsync(OnboardingCommand request, Guid employeeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000) throw new StaffOnboardingException("REASON_REQUIRED", 400);
        var employee = await context.Employees.SingleOrDefaultAsync(item => item.UserId == employeeId, cancellationToken)
            ?? throw new StaffOnboardingException("EMPLOYEE_NOT_FOUND", 404);
        if (!employee.CorrectionRequestId.HasValue || employee.CorrectionRevisionId.HasValue)
            throw new StaffOnboardingException("CORRECTION_NOT_REQUESTED");
        StaffOnboardingValidation.CheckVersion(employee.CorrectionVersion, request.ExpectedVersion);
        var latest = await EmployeeRevisions(employeeId).OrderByDescending(item => item.RevisionNumber).FirstOrDefaultAsync(cancellationToken);
        var profile = latest is null ? new StaffLegalProfile() : Onboarding.Profile(latest.ProtectedProfile);
        var revision = new StaffOnboardingRevision { EmployeeId = employeeId, RevisionNumber = (latest?.RevisionNumber ?? 0) + 1,
            ProtectedProfile = Onboarding.ProtectProfile(profile), CorrectionReason = request.Reason.Trim(), CreatedAtUtc = OnboardingNow };
        context.StaffOnboardingRevisions.Add(revision);
        employee.CorrectionRevisionId = revision.Id;
        employee.CorrectionVersion++;
        return revision;
    }

    private async Task CloseCorrectionAsync(Guid revisionId, CancellationToken cancellationToken)
    {
        var employee = await context.Employees.SingleOrDefaultAsync(item => item.CorrectionRevisionId == revisionId, cancellationToken);
        if (employee is not null)
        {
            employee.CorrectionRequestId = null;
            employee.CorrectionReason = null;
            employee.CorrectionRequestedAtUtc = null;
            employee.CorrectionRevisionId = null;
            employee.CorrectionVersion++;
        }
    }

    private async Task ReleaseIdentityReservationsAsync(Guid? invitationId, Guid? employeeId, CancellationToken cancellationToken)
    {
        var owner = employeeId ?? invitationId;
        if (!owner.HasValue) return;
        var reservations = await context.StaffOnboardingRevisions.Where(item => item.ReservedCccdDigest != null &&
            (item.EmployeeId == owner || context.StaffInvitations.Any(invitation => invitation.Id == item.InvitationId &&
                (invitation.CompletedUserId ?? invitation.Id) == owner))).ToListAsync(cancellationToken);
        foreach (var reservation in reservations.Where(item => item.State != "APPROVED"))
            reservation.ReservedCccdDigest = null;
    }

    private StaffInvitationView InvitationView(StaffInvitation invitation) => new(invitation.Id, invitation.Email, invitation.TargetRole,
        invitation.JoinDate, invitation.Status, invitation.DeliveryStatus, invitation.DeliveryErrorCode, invitation.ExpiresAtUtc,
        invitation.CreatedAtUtc, invitation.CompletedUserId, invitation.Version,
        invitation.ContractJson is null ? null : ScheduleDeserialize<ContractTerms>(invitation.ContractJson));

    private async Task<StaffRevisionView?> RevisionViewAsync(StaffOnboardingRevision? revision, CancellationToken cancellationToken)
    {
        if (revision is null) return null;
        var documents = await context.StaffOnboardingDocuments.AsNoTracking().Where(item => item.RevisionId == revision.Id).OrderBy(item => item.UploadedAtUtc).ToListAsync(cancellationToken);
        var reviewerName = revision.ReviewerId.HasValue
            ? await context.Users.AsNoTracking().Where(item => item.Id == revision.ReviewerId)
                .Select(item => item.UserProfile != null && item.UserProfile.FullName.Trim() != ""
                    ? item.UserProfile.FullName : item.Username).SingleOrDefaultAsync(cancellationToken)
            : null;
        return new(revision.Id, revision.RevisionNumber, revision.State, revision.Version, Onboarding.Profile(revision.ProtectedProfile),
            documents.Select(item => new StaffDocumentView(item.Id, item.Purpose,
                item.DeletionState == "AVAILABLE" && item.DeleteAfterUtc <= OnboardingNow ? "EXPIRED" : item.DeletionState == "PENDING" ? "PENDING_DELETION" : item.DeletionState,
                item.ContentType, item.UploadedAtUtc, item.DeleteAfterUtc, item.DeletedAtUtc, item.LastErrorCode)).ToList(),
            revision.SubmittedAtUtc, revision.ReviewedAtUtc, revision.ReviewerId, revision.CorrectionReason, revision.DocumentsProvided, reviewerName);
    }

    private async Task<OnboardingWorkspace> WorkspaceAsync(string code, StaffInvitation? invitation, StaffOnboardingRevision? revision,
        Guid? employeeId, CancellationToken cancellationToken)
    {
        var approved = employeeId.HasValue ? await EmployeeRevisions(employeeId.Value).Where(item => item.State == "APPROVED")
            .OrderByDescending(item => item.RevisionNumber).FirstOrDefaultAsync(cancellationToken) : null;
        var correction = employeeId.HasValue ? await context.Employees.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == employeeId, cancellationToken) : null;
        var contractJson = employeeId.HasValue
            ? await AppliedPatterns.AsNoTracking().Where(item => item.EmployeeId == employeeId)
                .OrderByDescending(item => item.AppliedVersion).Select(item => item.ContractJson).FirstOrDefaultAsync(cancellationToken)
            : null;
        var contract = contractJson is not null ? ScheduleDeserialize<ContractTerms>(contractJson)
            : invitation?.ContractJson is not null ? ScheduleDeserialize<ContractTerms>(invitation.ContractJson) : null;
        var shifts = await ContractShiftsAsync(contract, cancellationToken);
        var invitationView = invitation is null ? null : InvitationView(invitation);
        if (invitationView is not null)
            invitationView = invitationView with { Shifts = await ContractShiftsAsync(invitationView.Contract, cancellationToken) };
        return new(code, invitationView, await RevisionViewAsync(revision, cancellationToken),
            await RevisionViewAsync(approved, cancellationToken), correction?.CorrectionRequestId is null ? null : new(correction.CorrectionRequestId.Value, correction.UserId,
                correction.CorrectionReason!, correction.CorrectionRevisionId.HasValue ? "OPENED" : "REQUESTED", correction.CorrectionRequestedAtUtc!.Value,
                correction.CorrectionRevisionId, correction.CorrectionVersion),
            Contract: contract, Shifts: shifts);
    }

    private async Task<IReadOnlyList<ContractShiftView>> ContractShiftsAsync(ContractTerms? contract, CancellationToken cancellationToken)
    {
        var ids = contract?.WeeklyPattern.SelectMany(item => item.ShiftIds).Distinct().ToArray() ?? [];
        return await context.Shifts.AsNoTracking().Where(item => ids.Contains(item.Id)).OrderBy(item => item.StartTime)
            .Select(item => new ContractShiftView(item.Id, item.Name, item.StartTime, item.EndTime, item.IsActive == true))
            .ToListAsync(cancellationToken);
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

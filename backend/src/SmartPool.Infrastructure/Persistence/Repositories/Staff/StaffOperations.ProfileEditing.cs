using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffEdit;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    public async Task<GetStaffEditResponse> GetStaffEditAsync(GetStaffEditQuery request, CancellationToken cancellationToken)
    {
        await RequireOnboardingAdminAsync(request.ActorId, cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var employee = await context.Employees.AsNoTracking().Include(item => item.User.UserProfile)
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân viên.");
        var approved = await ApprovedEditRevisionAsync(request.UserId, cancellationToken);
        var legal = approved is null ? null : Onboarding.Profile(approved.ProtectedProfile);
        var profile = employee.User.UserProfile;
        var blocked = await DetailsEditingBlockedAsync(employee, cancellationToken);
        var result = new GetStaffEditResponse(employee.UserId, employee.Status,
            new StaffEditValues(profile?.FullName, employee.User.Phone, profile?.Address, profile?.DateOfBirth,
                legal?.CccdNumber, legal?.PermanentAddress, legal?.TemporaryAddress, legal?.BankName,
                legal?.BankAccountNumber, legal?.TaxIdentifierKind, legal?.TaxIdentifier),
            employee.UpdatedAt, employee.User.UpdatedAt, profile?.UpdatedAt, profile is not null,
            approved?.Id, approved?.Version, employee.CorrectionVersion, blocked);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<UpdateStaffResponse> UpdateStaffAsync(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (request.Details is not null)
        {
            await LockOnboardingAsync(cancellationToken);
            await RequireOnboardingAdminAsync(request.ActorId, cancellationToken);
        }
        await LockEmployeesAsync([request.UserId], cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.users WHERE id = {request.UserId} FOR UPDATE", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT user_id FROM smart_pool.user_profiles WHERE user_id = {request.UserId} FOR UPDATE", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT user_id FROM smart_pool.employees WHERE user_id = {request.UserId} FOR UPDATE", cancellationToken);
        var employee = await context.Employees.Include(item => item.User.UserProfile).Include(item => item.User.Role)
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân viên.");

        if (request.Details is not null)
            await ApplyStaffDetailsAsync(request, employee, cancellationToken);
        employee.Status = request.Status;
        employee.UpdatedAt = OnboardingNow;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new StaffOnboardingException("STALE_VERSION"); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" } postgres)
        {
            throw new StaffOnboardingException(postgres.ConstraintName == "users_phone_key" ? "PHONE_ALREADY_EXISTS" : "IDENTITY_CONFLICT");
        }
        return mapper.Map<UpdateStaffResponse>(employee);
    }

    private async Task ApplyStaffDetailsAsync(UpdateStaffCommand request, Employee employee, CancellationToken cancellationToken)
    {
        var approved = await ApprovedEditRevisionAsync(employee.UserId, cancellationToken);
        var user = employee.User;
        var profile = user.UserProfile;
        if (!request.HasEditSnapshot || request.ExpectedUpdatedAt != employee.UpdatedAt ||
            request.ExpectedUserUpdatedAt != user.UpdatedAt || request.ExpectedProfileUpdatedAt != profile?.UpdatedAt ||
            request.ExpectedProfileExists != (profile is not null) || request.ExpectedApprovedRevisionId != approved?.Id ||
            request.ExpectedApprovedRevisionVersion != approved?.Version || request.ExpectedCorrectionVersion != employee.CorrectionVersion)
            throw new StaffOnboardingException("STALE_VERSION");
        if (await DetailsEditingBlockedAsync(employee, cancellationToken))
            throw new StaffOnboardingException("CORRECTION_IN_PROGRESS");

        var legal = approved is null ? new StaffLegalProfile { TaxIdentifierKind = "" } : Onboarding.Profile(approved.ProtectedProfile);
        var current = legal with { FullName = profile?.FullName ?? "", Phone = user.Phone ?? "", DateOfBirth = profile?.DateOfBirth };
        var details = request.Details!;
        var merged = details.Merge(current);
        await StaffEditValidation.ValidateAsync(merged, timeProvider, cancellationToken);
        await CheckPhoneAsync(StaffOnboardingValidation.Phone(merged.Phone), employee.UserId, cancellationToken);

        var legalChanged = approved is not null ? merged != current :
            merged with { FullName = current.FullName, Phone = current.Phone, DateOfBirth = current.DateOfBirth } != current;
        if (legalChanged)
            await AppendStaffEditRevisionAsync(request, merged, cancellationToken);

        if (user.Phone != merged.Phone)
        {
            user.Phone = merged.Phone;
            user.UpdatedAt = OnboardingNow;
        }
        var address = details.Has(nameof(details.Address)) ? details.Address?.Trim() : profile?.Address;
        if (details.Has(nameof(details.Address)) && string.IsNullOrEmpty(address)) address = null;
        if (profile is null)
        {
            profile = new UserProfile { UserId = user.Id, User = user };
            context.UserProfiles.Add(profile);
        }
        if (profile.FullName != merged.FullName || profile.DateOfBirth != merged.DateOfBirth || profile.Address != address)
        {
            profile.FullName = merged.FullName;
            profile.DateOfBirth = merged.DateOfBirth;
            profile.Address = address;
            profile.UpdatedAt = OnboardingNow;
        }
    }

    private async Task AppendStaffEditRevisionAsync(UpdateStaffCommand request, StaffLegalProfile profile, CancellationToken cancellationToken)
    {
        string? digest = null;
        string? reservedDigest = null;
        if (profile.CccdNumber.Length > 0)
        {
            digest = Onboarding.IdentityDigest(profile.CccdNumber);
            var holder = await context.StaffOnboardingRevisions.Where(item => item.ReservedCccdDigest == digest)
                .Select(item => new
                {
                    OwnerId = item.EmployeeId ?? context.StaffInvitations.Where(invitation => invitation.Id == item.InvitationId)
                        .Select(invitation => invitation.CompletedUserId ?? invitation.Id).Single()
                }).SingleOrDefaultAsync(cancellationToken);
            if (holder is not null && holder.OwnerId != request.UserId) throw new StaffOnboardingException("IDENTITY_CONFLICT");
            if (holder is null) reservedDigest = digest;
        }
        var latestNumber = await EmployeeRevisions(request.UserId).MaxAsync(item => (int?)item.RevisionNumber, cancellationToken) ?? 0;
        context.StaffOnboardingRevisions.Add(new StaffOnboardingRevision
        {
            EmployeeId = request.UserId, RevisionNumber = latestNumber + 1, State = "APPROVED",
            ProtectedProfile = Onboarding.ProtectProfile(profile), CccdDigest = digest, ReservedCccdDigest = reservedDigest,
            CreatedAtUtc = OnboardingNow, ReviewedAtUtc = OnboardingNow, ReviewerId = request.ActorId,
            CorrectionReason = "ADMIN_DIRECT_CORRECTION", DocumentsProvided = false
        });
    }

    private Task<StaffOnboardingRevision?> ApprovedEditRevisionAsync(Guid employeeId, CancellationToken cancellationToken) =>
        EmployeeRevisions(employeeId).AsNoTracking().Where(item => item.State == "APPROVED")
            .OrderByDescending(item => item.RevisionNumber).FirstOrDefaultAsync(cancellationToken);

    private Task<bool> DetailsEditingBlockedAsync(Employee employee, CancellationToken cancellationToken) =>
        employee.CorrectionRequestId.HasValue || employee.CorrectionRevisionId.HasValue
            ? Task.FromResult(true)
            : EmployeeRevisions(employee.UserId).AnyAsync(item => item.State == "DRAFT" || item.State == "SUBMITTED" ||
                item.State == "CHANGES_REQUESTED", cancellationToken);
}

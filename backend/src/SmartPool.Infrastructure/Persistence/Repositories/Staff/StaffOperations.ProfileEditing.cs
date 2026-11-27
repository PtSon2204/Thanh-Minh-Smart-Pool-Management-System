using System.Data;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;
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
        var record = await context.StaffLegalProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EmployeeId == request.UserId, cancellationToken);
        var legal = record is null ? null : Onboarding.Profile(record.ProtectedProfile);
        var profile = employee.User.UserProfile;
        var result = new GetStaffEditResponse(employee.UserId, employee.Status,
            new StaffEditValues(profile?.FullName, employee.User.Phone, profile?.Address, profile?.DateOfBirth,
                legal?.CccdNumber, legal?.PermanentAddress, legal?.TemporaryAddress, legal?.BankName,
                legal?.BankAccountNumber, legal?.TaxIdentifierKind, legal?.TaxIdentifier,
                profile?.Gender, employee.User.Email, legal?.EmergencyContactName, legal?.EmergencyContactPhone,
                legal?.EmergencyContactRelationship),
            employee.UpdatedAt, employee.User.UpdatedAt, profile?.UpdatedAt, profile is not null,
            record?.Version);
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
        {
            if (!string.IsNullOrEmpty(request.Status) && request.Status != employee.Status)
                throw new ValidationException([new ValidationFailure("Status", "Hãy thay đổi trạng thái nhân viên riêng với hồ sơ.")]);
            await ApplyStaffDetailsAsync(request, employee, cancellationToken);
        }
        if (request.Details is null)
        {
        var reactivating = request.Status == "Working" && (employee.Status != "Working" ||
            !string.Equals(employee.User.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase));
        employee.Status = request.Status;
        employee.ContractInactiveSinceUtc = null;
        employee.UpdatedAt = OnboardingNow;
        if (employee.User.Role is not null &&
            (string.Equals(employee.User.Role.Name, "SALES", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(employee.User.Role.Name, "LIFEGUARD", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(employee.User.Role.Name, "COACH", StringComparison.OrdinalIgnoreCase)))
        {
            if (request.Status == "Inactive" || reactivating)
            {
                employee.ManualAccessEnabled = reactivating;
                employee.User.Status = reactivating ? "ACTIVE" : "LOCKED";
                employee.User.UpdatedAt = OnboardingNow;
            }
        }
        }
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new StaffOnboardingException("STALE_VERSION"); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" } postgres)
        {
            if (postgres.ConstraintName == "users_phone_key") throw new StaffOnboardingException("PHONE_ALREADY_EXISTS");
            if (postgres.ConstraintName == "staff_legal_profiles_cccd_digest_key") throw CccdAlreadyUsed();
            if (postgres.ConstraintName is "users_email_key" or "users_email_lower_key" or "users_normalized_email_onboarding_key") throw EditEmailConflict();
            throw new StaffOnboardingException("IDENTITY_CONFLICT");
        }
        return mapper.Map<UpdateStaffResponse>(employee);
    }

    private async Task ApplyStaffDetailsAsync(UpdateStaffCommand request, Employee employee, CancellationToken cancellationToken)
    {
        var record = await context.StaffLegalProfiles.SingleOrDefaultAsync(item => item.EmployeeId == employee.UserId, cancellationToken);
        var user = employee.User;
        var profile = user.UserProfile;
        if (!request.HasEditSnapshot || request.ExpectedUpdatedAt != employee.UpdatedAt ||
            request.ExpectedUserUpdatedAt != user.UpdatedAt || request.ExpectedProfileUpdatedAt != profile?.UpdatedAt ||
            request.ExpectedProfileExists != (profile is not null) || request.ExpectedLegalProfileVersion != record?.Version)
            throw new StaffOnboardingException("STALE_VERSION");

        var legal = record is null ? new StaffLegalProfile { TaxIdentifierKind = "" } : Onboarding.Profile(record.ProtectedProfile);
        var current = legal with { FullName = profile?.FullName ?? "", Phone = user.Phone ?? "", DateOfBirth = profile?.DateOfBirth };
        var details = request.Details!;
        var merged = details.Merge(current);
        await StaffEditValidation.ValidateAsync(merged, details, timeProvider, cancellationToken);
        if (details.Has(nameof(details.Phone))) await CheckPhoneAsync(StaffOnboardingValidation.Phone(merged.Phone), employee.UserId, cancellationToken);

        var addFields = new List<string>();
        if (details.Has(nameof(details.Gender))) addFields.Add(nameof(AddEmployeeCommand.Gender));
        if (details.Has(nameof(details.PersonalEmail))) addFields.Add(nameof(AddEmployeeCommand.PersonalEmail));
        var email = details.PersonalEmail?.Trim().ToLowerInvariant();
        if (addFields.Count > 0)
        {
            var validation = await new AddEmployeeValidator(timeProvider).ValidateAsync(
                new AddEmployeeCommand { Gender = details.Gender ?? "", PersonalEmail = email ?? "" },
                options => options.IncludeProperties(addFields.ToArray()), cancellationToken);
            if (!validation.IsValid) throw new ValidationException(validation.Errors.Select(error =>
                new ValidationFailure($"Details.{error.PropertyName}", error.ErrorMessage)));
        }
        if (details.Has(nameof(details.PersonalEmail)) && await context.Users.AsNoTracking().AnyAsync(
            item => item.Id != user.Id && item.Email != null && item.Email.Trim().ToLower() == email, cancellationToken))
            throw EditEmailConflict();

        var legalChanged = record is not null ? merged != current :
            merged with { FullName = current.FullName, Phone = current.Phone, DateOfBirth = current.DateOfBirth } != current;
        if (legalChanged)
            await UpsertLegalProfileAsync(request, merged, record, cancellationToken);

        if ((details.Has(nameof(details.Phone)) && user.Phone != merged.Phone) || (details.Has(nameof(details.PersonalEmail)) && user.Email != email))
        {
            if (details.Has(nameof(details.Phone))) user.Phone = merged.Phone;
            if (details.Has(nameof(details.PersonalEmail))) user.Email = email;
            user.UpdatedAt = OnboardingNow;
        }
        var address = details.Has(nameof(details.Address)) ? details.Address?.Trim() : profile?.Address;
        if (details.Has(nameof(details.Address)) && string.IsNullOrEmpty(address)) address = null;
        if (profile is null && (details.Has(nameof(details.FullName)) || details.Has(nameof(details.DateOfBirth)) || address is not null || details.Has(nameof(details.Gender))))
        {
            if (!details.Has(nameof(details.FullName)))
                throw new ValidationException([new ValidationFailure("Details.FullName", "Nhập họ và tên để tạo hồ sơ.")]);
            profile = new UserProfile { UserId = user.Id, User = user, FullName = merged.FullName, UpdatedAt = OnboardingNow };
            context.UserProfiles.Add(profile);
        }
        if (profile is not null && ((details.Has(nameof(details.FullName)) && profile.FullName != merged.FullName) || (details.Has(nameof(details.DateOfBirth)) && profile.DateOfBirth != merged.DateOfBirth) || (details.Has(nameof(details.Address)) && profile.Address != address) ||
            (details.Has(nameof(details.Gender)) && profile.Gender != details.Gender)))
        {
            if (details.Has(nameof(details.FullName))) profile.FullName = merged.FullName;
            if (details.Has(nameof(details.DateOfBirth))) profile.DateOfBirth = merged.DateOfBirth;
            if (details.Has(nameof(details.Address))) profile.Address = address;
            if (details.Has(nameof(details.Gender))) profile.Gender = details.Gender;
            profile.UpdatedAt = OnboardingNow;
        }
    }

    private async Task UpsertLegalProfileAsync(UpdateStaffCommand request, StaffLegalProfile profile,
        StaffLegalProfileRecord? record, CancellationToken cancellationToken)
    {
        string? digest = null;
        if (profile.CccdNumber.Length > 0)
        {
            digest = Onboarding.IdentityDigest(profile.CccdNumber);
            var holder = await context.StaffLegalProfiles.AsNoTracking()
                .Where(item => item.CccdDigest == digest && item.EmployeeId != request.UserId)
                .Select(item => (Guid?)item.EmployeeId)
                .SingleOrDefaultAsync(cancellationToken);
            if (holder is not null) throw CccdAlreadyUsed();
        }
        if (record is null)
        {
            context.StaffLegalProfiles.Add(new StaffLegalProfileRecord
            {
                EmployeeId = request.UserId, ProtectedProfile = Onboarding.ProtectProfile(profile),
                CccdDigest = digest, CreatedAtUtc = OnboardingNow
            });
            return;
        }
        record.ProtectedProfile = Onboarding.ProtectProfile(profile);
        record.CccdDigest = digest;
        record.UpdatedAtUtc = OnboardingNow;
        record.Version++;
    }

    private static ValidationException CccdAlreadyUsed() =>
        new([new ValidationFailure("Details.CccdNumber", "Số CCCD đã được nhân viên khác sử dụng.")]);

    private static ValidationException EditEmailConflict() =>
        new([new ValidationFailure("Details.PersonalEmail", "Email cá nhân đã được sử dụng.")]);

}

using System.Globalization;
using System.Net;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    // Plan §3.3: try suffixes 000-999 before reporting the company ID as unavailable.
    private const int CompanyEmailRetryLimit = 1000;
    private const int TempPasswordExpiresHours = 24;
    private const string CredentialsEmailSubject = "Tài khoản SmartPool của bạn";
    private const string EmailDeliverySent = "SENT";
    private const string EmailDeliveryFailed = "FAILED";

    public async Task<AddEmployeeResponse> AddEmployeeAsync(AddEmployeeCommand request, CancellationToken cancellationToken)
    {
        await RequireOnboardingAdminAsync(request.ActorId, cancellationToken);
        var role = await context.Roles.AsNoTracking()
            .Where(item => EF.Functions.ILike(item.Name, request.TargetRole) && item.IsDeleted != true)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new StaffOnboardingException("ROLE_UNAVAILABLE", 400);
        var legal = BuildLegalProfile(request);
        await ValidateContractAsync(request.Contract, request.JoinDate, cancellationToken);
        await CheckPhoneAsync(legal.Phone, null, cancellationToken);
        var personalEmail = request.PersonalEmail.Trim().ToLowerInvariant();
        if (await context.Users.AsNoTracking().AnyAsync(item => item.Email != null && item.Email.Trim().ToLower() == personalEmail, cancellationToken))
            throw PersonalEmailConflict();
        string? digest = null;
        if (legal.CccdNumber.Length > 0)
        {
            digest = Onboarding.IdentityDigest(legal.CccdNumber);
            if (await context.StaffLegalProfiles.AsNoTracking().AnyAsync(item => item.CccdDigest == digest, cancellationToken))
                throw CccdConflict();
        }
        var now = OnboardingNow;
        var temporaryPassword = TempPasswords.Generate();
        var passwordHash = Onboarding.Passwords.HashPassword(new User { Id = Guid.NewGuid() }, temporaryPassword);
        var attemptedCompanyEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var attempt = 0; attempt < CompanyEmailRetryLimit; attempt++)
        {
            var companyEmail = await CompanyEmails.GenerateAsync(legal.FullName, attemptedCompanyEmails, cancellationToken);
            attemptedCompanyEmails.Add(companyEmail);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var user = new User
                {
                    Id = Guid.NewGuid(), Email = personalEmail, Username = null, Phone = legal.Phone,
                    RoleId = role.Id, Status = "Active", IsDeleted = false,
                    MustChangePassword = true, TemporaryPasswordExpiresAtUtc = now.AddHours(TempPasswordExpiresHours),
                    PasswordHash = passwordHash, CreatedAt = now, UpdatedAt = now
                };
                context.Users.Add(user);
                context.UserProfiles.Add(new UserProfile
                {
                    UserId = user.Id, FullName = legal.FullName, DateOfBirth = legal.DateOfBirth,
                    Gender = request.Gender, UpdatedAt = now
                });
                var employee = new Employee
                {
                    UserId = user.Id, User = user, JoinDate = request.JoinDate,
                    CompanyEmail = companyEmail, Status = "Working", CreatedAt = now, UpdatedAt = now
                };
                context.Employees.Add(employee);
                context.StaffLegalProfiles.Add(new StaffLegalProfileRecord
                {
                    EmployeeId = user.Id, ProtectedProfile = Onboarding.ProtectProfile(legal),
                    CccdDigest = digest, CreatedAtUtc = now
                });
                // Initial applied contract in the same transaction (plan §3.5). A failed
                // contract rolls back the account and sends no credentials.
                await ApplyInitialContractAsync(employee, request.Contract!, user.Id, request.ActorId, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                var delivery = await SendEmployeeCredentialsAsync(legal.FullName, personalEmail, companyEmail,
                    temporaryPassword, user.TemporaryPasswordExpiresAtUtc!.Value, CredentialsEmailSubject, cancellationToken);
                return new AddEmployeeResponse(user.Id, companyEmail, delivery);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: "23505" } postgres &&
                postgres.ConstraintName == "employees_company_email_key")
            {
                // A concurrent insert claimed this suffix; drop the failed entities,
                // roll back and try another suffix (plan §3.3).
                context.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: "23505" } postgres &&
                postgres.ConstraintName is "users_email_key" or "users_email_lower_key" or "users_normalized_email_onboarding_key")
            {
                throw PersonalEmailConflict();
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: "23505", ConstraintName: "users_phone_key" })
            {
                throw new ValidationException([new ValidationFailure("Phone", "Số điện thoại đã được sử dụng.")]);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: "23505", ConstraintName: "staff_legal_profiles_cccd_digest_key" })
            {
                throw CccdConflict();
            }
        }
        throw new StaffOnboardingException("COMPANY_EMAIL_UNAVAILABLE", 409);
    }

    private static StaffLegalProfile BuildLegalProfile(AddEmployeeCommand request) => new()
    {
        FullName = request.FullName.Trim(),
        Phone = StaffOnboardingValidation.Phone(request.Phone),
        DateOfBirth = request.DateOfBirth,
        CccdNumber = request.CccdNumber.Trim(),
        PermanentAddress = request.PermanentAddress.Trim(),
        TemporaryAddress = request.TemporaryAddress?.Trim() ?? "",
        BankName = "",
        BankAccountNumber = "",
        TaxIdentifierKind = "PERSONAL_ID",
        TaxIdentifier = request.CccdNumber.Trim(),
        EmergencyContactName = "",
        EmergencyContactPhone = "",
        EmergencyContactRelationship = ""
    };

    private static ValidationException CccdConflict() =>
        new([new ValidationFailure("CccdNumber", "Số CCCD đã được nhân viên khác sử dụng.")]);

    private static ValidationException PersonalEmailConflict() =>
        new([new ValidationFailure("PersonalEmail", "Email cá nhân đã được sử dụng.")]);

    // Plan §3.6: send the temporary credential to the personal address after commit.
    // The password is never stored, logged or returned; delivery is SENT or FAILED.
    private async Task<string> SendEmployeeCredentialsAsync(string fullName, string personalEmail,
        string companyEmail, string temporaryPassword, DateTime expiresAtUtc, string subject,
        CancellationToken cancellationToken)
    {
        try
        {
        var loginUrl = Onboarding.LoginLink();
        var expiry = expiresAtUtc.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var body =
            $"<p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>" +
            "<p>Thông tin đăng nhập SmartPool của bạn:</p>" +
            "<ul>" +
            $"<li>Email công ty (dùng để đăng nhập): {WebUtility.HtmlEncode(companyEmail)}</li>" +
            $"<li>Mật khẩu tạm thời: {WebUtility.HtmlEncode(temporaryPassword)}</li>" +
            $"<li>Mật khẩu tạm thời có hiệu lực đến: {WebUtility.HtmlEncode(expiry)}</li>" +
            "</ul>" +
            "<p>Bạn phải đổi mật khẩu ngay sau khi đăng nhập lần đầu.</p>" +
            $"<p>Đăng nhập tại: <a href=\"{WebUtility.HtmlEncode(loginUrl)}\">{WebUtility.HtmlEncode(loginUrl)}</a></p>";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await Onboarding.Email.SendAsync(personalEmail, subject, body, timeout.Token);
            return EmailDeliverySent;
        }
        catch { return EmailDeliveryFailed; }
    }
}

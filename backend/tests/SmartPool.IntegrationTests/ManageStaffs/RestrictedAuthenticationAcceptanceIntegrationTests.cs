using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using SmartPool.Application.Features.Authentication.Commands.ChangePassword;
using SmartPool.Application.Features.Authentication.Commands.Login;
using SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;
using SmartPool.Application.Features.ManageStaffs.Mapping;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using SmartPool.Infrastructure.Persistence.Repositories.Users;
using SmartPool.Infrastructure.Services;
using SmartPool.Infrastructure.StaffOnboarding;

namespace SmartPool.IntegrationTests.ManageStaffs;

// Plan §7 check 2 (bounded, fake email/storage). Verifies restricted sign-in,
// forced change, re-login, admin reset and customer sign-in at handler/service level.
public sealed class RestrictedAuthenticationAcceptanceIntegrationTests
{
    private const string NewPassword = "NewSynthetic123";

    [Fact]
    public async Task RestrictedSignInForcesChangeAndCannotBeReused()
    {
        await using var context = await Setup.CreateContextAsync();
        var mail = new RecordingMail();
        var wiring = await Setup.WireAsync(context, mail);
        var personalEmail = $"auth-{Guid.NewGuid():N}@example.test"; var (userId, companyEmail) = await wiring.CreateEmployeeAsync(personalEmail);
        var role = "SALES";

        // 1. Company identifier + temporary password authenticates.
        var login = await wiring.Login(companyEmail, mail.LastTemporaryPassword);
        Assert.Equal(userId, login.Id);
        Assert.True(login.MustChangePassword);
        Assert.True(string.IsNullOrEmpty(login.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        // 2. Restricted session has no refresh token row.
        Assert.Equal(0, await context.RefreshTokens.CountAsync(item => item.UserId == userId));

        // 3. Restricted token is short-lived and carries the purpose claim.
        var token = new JsonWebToken(login.AccessToken);
        Assert.Equal("password_change", token.GetClaim("purpose").Value);
        var passwordVersion = token.GetClaim("pwd_v").Value;
        Assert.True(token.ValidTo - token.ValidFrom <= TimeSpan.FromMinutes(10));

        // Restricted tokens are rejected for normal validation; normal tokens are
        // rejected while the flag is set. (The HTTP middleware gate itself is not
        // executed here: no WebApplicationFactory in this project.)
        var restrictedValid = await wiring.AccessTokens.IsPasswordChangeCurrentAsync(userId, [role], passwordVersion, default);
        var normalValid = await wiring.AccessTokens.IsCurrentRolesAsync(userId, [role], passwordVersion, default);
        Assert.True(restrictedValid);
        Assert.False(normalValid);
        // Refresh issuance is blocked while the flag is set.
        var flaggedHash = (await context.Users.AsNoTracking().SingleAsync(item => item.Id == userId)).PasswordHash;
        Assert.Null(await wiring.RefreshTokens.CreateAsync(userId, flaggedHash, default));

        // 5. Change password with the temporary credential.
        var change = await wiring.ChangePassword(userId, mail.LastTemporaryPassword);
        Assert.IsType<NoContentResult>(change);
        var user = await context.Users.AsNoTracking().SingleAsync(item => item.Id == userId);
        Assert.False(user.MustChangePassword);
        Assert.Null(user.TemporaryPasswordExpiresAtUtc);

        // 6. The previous restricted token cannot be reused (fingerprint changed).
        Assert.False(await wiring.AccessTokens.IsPasswordChangeCurrentAsync(userId, [role], passwordVersion, default));

        // 7. Employee signs in again with the new password; temporary password fails.
        var relogin = await wiring.Login(companyEmail, NewPassword);
        Assert.False(relogin.MustChangePassword);
        Assert.NotNull(relogin.RefreshToken);
        Assert.NotNull(relogin.AccessToken);
        await Assert.ThrowsAsync<InvalidCredentialsFailure>(() => wiring.Login(companyEmail, mail.LastTemporaryPassword));
    }

    [Fact]
    public async Task AdminResetInvalidatesCredentialsMailsPersonalAddressAndEnforcesRoles()
    {
        await using var context = await Setup.CreateContextAsync();
        var mail = new RecordingMail();
        var wiring = await Setup.WireAsync(context, mail);
        var personalEmail = $"auth-{Guid.NewGuid():N}@example.test"; var (userId, companyEmail) = await wiring.CreateEmployeeAsync(personalEmail);
        await wiring.ChangePassword(userId, mail.LastTemporaryPassword);
        var normal = await wiring.Login(companyEmail, NewPassword);
        Assert.NotNull(normal.RefreshToken);

        // 8+9. Admin reset revokes access/refresh credentials and mails the saved address.
        mail.Reset();
        var reset = await wiring.AdminReset(userId, personalEmail);
        Assert.Equal("SENT", reset.EmailDelivery);
        Assert.Equal(personalEmail, Assert.Single(mail.Recipients));
        Assert.Equal("Đặt lại mật khẩu SmartPool", mail.Subject);
        Assert.All(await context.RefreshTokens.Where(item => item.UserId == userId).ToListAsync(), item => Assert.NotNull(item.RevokeAt));
        Assert.False(await wiring.AccessTokens.IsCurrentAsync(userId, "SALES", new JsonWebToken(normal.AccessToken).GetClaim("pwd_v").Value, default));
        var afterReset = await context.Users.AsNoTracking().SingleAsync(item => item.Id == userId);
        Assert.True(afterReset.MustChangePassword);
        Assert.InRange(afterReset.TemporaryPasswordExpiresAtUtc!.Value, DateTime.UtcNow.AddHours(23), DateTime.UtcNow.AddHours(25));
        await Assert.ThrowsAsync<InvalidCredentialsFailure>(() => wiring.Login(companyEmail, NewPassword));
        var restricted = await wiring.Login(companyEmail, mail.LastTemporaryPassword);
        Assert.True(restricted.MustChangePassword);

        // 10. A non-admin actor cannot reset employee passwords.
        var saler = await context.Users.FirstAsync(item => item.Role!.Name == "SALES" && item.Employee != null && item.Id == userId);
        var forbidden = await Assert.ThrowsAsync<StaffOnboardingException>(() => wiring.AdminReset(userId, personalEmail, saler.Id));
        Assert.Equal("ADMIN_REQUIRED", forbidden.Code);
        // Customers are not employee reset targets.
        var customer = await wiring.CreateCustomerAsync();
        var wrongTarget = await Assert.ThrowsAsync<StaffOnboardingException>(() => wiring.AdminReset(customer.Id, "customer@example.test"));
        Assert.Contains(wrongTarget.Code, new[] { "EMPLOYEE_NOT_FOUND", "EMPLOYEE_REQUIRED" });

        // 11. Ordinary customer authentication still works.
        var customerLogin = await wiring.Login("synthetic-customer", "Customer123!");
        Assert.False(customerLogin.MustChangePassword);
        Assert.NotNull(customerLogin.RefreshToken);
    }

    [Fact]
    public async Task FailedResetDeliveryStillInvalidatesOldCredentialsAndCanBeReplaced()
    {
        await using var context = await Setup.CreateContextAsync();
        var mail = new RecordingMail();
        var wiring = await Setup.WireAsync(context, mail);
        var email = $"delivery-{Guid.NewGuid():N}@example.test";
        var (userId, companyEmail) = await wiring.CreateEmployeeAsync(email);
        await wiring.ChangePassword(userId, mail.LastTemporaryPassword);
        var normal = await wiring.Login(companyEmail, NewPassword);
        mail.FailDelivery = true;
        var failed = await wiring.AdminReset(userId, email);
        Assert.Equal("FAILED", failed.EmailDelivery);
        await Assert.ThrowsAsync<InvalidCredentialsFailure>(() => wiring.Login(companyEmail, NewPassword));
        Assert.False(await wiring.AccessTokens.IsCurrentAsync(userId, "SALES",
            new JsonWebToken(normal.AccessToken).GetClaim("pwd_v").Value, default));
        Assert.All(await context.RefreshTokens.Where(item => item.UserId == userId).ToListAsync(), item => Assert.NotNull(item.RevokeAt));
        var failedPassword = mail.LastTemporaryPassword;
        mail.FailDelivery = false;
        Assert.Equal("SENT", (await wiring.AdminReset(userId, email)).EmailDelivery);
        await Assert.ThrowsAsync<InvalidCredentialsFailure>(() => wiring.Login(companyEmail, failedPassword));
        Assert.True((await wiring.Login(companyEmail, mail.LastTemporaryPassword)).MustChangePassword);
    }

    [Fact]
    public async Task PreContractCredentialSetupIsRestrictedAndDoesNotGrantBusinessAccess()
    {
        await using var context = await Setup.CreateContextAsync();
        var mail = new RecordingMail();
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var wiring = await Setup.WireAsync(context, mail, clock);
        var joinDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).Date).AddDays(5);
        var personalEmail = $"precontract-{Guid.NewGuid():N}@example.test";
        var (userId, companyEmail) = await wiring.CreateEmployeeAsync(personalEmail, joinDate);

        // 1-3. Future contract start still allows a restricted credential-setup session.
        var login = await wiring.Login(companyEmail, mail.LastTemporaryPassword);
        Assert.True(login.MustChangePassword);
        Assert.True(string.IsNullOrEmpty(login.RefreshToken));
        Assert.Equal(0, await context.RefreshTokens.CountAsync(item => item.UserId == userId));
        var token = new JsonWebToken(login.AccessToken);
        Assert.Equal("password_change", token.GetClaim("purpose").Value);
        var restrictedVersion = token.GetClaim("pwd_v").Value;

        // 4. Ordinary business authorization is unavailable (token level; HTTP
        // middleware is not executed in this harness).
        Assert.True(await wiring.AccessTokens.IsPasswordChangeCurrentAsync(userId, ["SALES"], restrictedVersion, default));
        Assert.False(await wiring.AccessTokens.IsCurrentRolesAsync(userId, ["SALES"], restrictedVersion, default));

        // 5-6. Password change succeeds and invalidates the previous restricted token.
        Assert.IsType<NoContentResult>(await wiring.ChangePassword(userId, mail.LastTemporaryPassword));
        Assert.False(await wiring.AccessTokens.IsPasswordChangeCurrentAsync(userId, ["SALES"], restrictedVersion, default));

        // 7. Normal login remains rejected before the contract starts.
        await Assert.ThrowsAsync<InvalidCredentialsFailure>(() => wiring.Login(companyEmail, NewPassword));

        // 8. Once the contract starts, normal authentication succeeds with the new password.
        clock.Now = new DateTimeOffset(joinDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7));
        var normal = await wiring.Login(companyEmail, NewPassword);
        Assert.False(normal.MustChangePassword);
        Assert.NotNull(normal.RefreshToken);
    }

    [Fact]
    public async Task CredentialSetupExceptionRespectsExpiryStatusAndAdminReset()
    {
        await using var context = await Setup.CreateContextAsync();
        var mail = new RecordingMail();
        var wiring = await Setup.WireAsync(context, mail);
        var joinDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).Date).AddDays(5);

        // 9. Expired temporary password stays rejected.
        var (expiredId, expiredEmail) = await wiring.CreateEmployeeAsync($"precontract-{Guid.NewGuid():N}@example.test", joinDate);
        var beforeExpiry = await wiring.Login(expiredEmail, mail.LastTemporaryPassword);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE smart_pool.users SET temporary_password_expires_at_utc = {DateTime.UtcNow.AddHours(-1)} WHERE id = {expiredId}");
        var expired = await wiring.LoginRaw(expiredEmail, mail.LastTemporaryPassword);
        Assert.IsType<UnauthorizedObjectResult>(expired);
        Assert.False(await wiring.AccessTokens.IsPasswordChangeCurrentAsync(expiredId, ["SALES"],
            new JsonWebToken(beforeExpiry.AccessToken).GetClaim("pwd_v").Value, default));
        Assert.IsType<UnauthorizedResult>(await wiring.ChangePassword(expiredId, mail.LastTemporaryPassword));

        // 10. Disabled and deleted accounts cannot use the exception.
        var (disabledId, disabledEmail) = await wiring.CreateEmployeeAsync($"precontract-{Guid.NewGuid():N}@example.test", joinDate);
        var disabledPassword = mail.LastTemporaryPassword;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE smart_pool.users SET status = 'LOCKED' WHERE id = {disabledId}");
        await Assert.ThrowsAsync<InvalidCredentialsFailure>(() => wiring.Login(disabledEmail, disabledPassword));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE smart_pool.users SET status = 'Active', is_deleted = true WHERE id = {disabledId}");
        await Assert.ThrowsAsync<InvalidCredentialsFailure>(() => wiring.Login(disabledEmail, disabledPassword));

        // 11. Admin reset before the contract starts keeps the same restrictions.
        var (resetId, resetEmail) = await wiring.CreateEmployeeAsync($"precontract-{Guid.NewGuid():N}@example.test", joinDate);
        var beforeReset = mail.Recipients.Count;
        var reset = await wiring.AdminReset(resetId, mail.LastRecipient);
        Assert.Equal("SENT", reset.EmailDelivery);
        Assert.Equal(beforeReset + 1, mail.Recipients.Count);
        var afterReset = await wiring.Login(resetEmail, mail.LastTemporaryPassword);
        Assert.True(afterReset.MustChangePassword);
        Assert.True(string.IsNullOrEmpty(afterReset.RefreshToken));
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Harness(
        SmartPoolDbContext context, RecordingMail mail, StaffOperations staff,
        UserOperations users, AccessTokenService accessTokens, RefreshTokenService refreshTokens,
        LoginHandler login, ChangePasswordHandler changePassword, Guid adminId)
    {
        public AccessTokenService AccessTokens { get; } = accessTokens;
        public RefreshTokenService RefreshTokens { get; } = refreshTokens;
        private Guid UserId { get; set; }

        public async Task<(Guid UserId, string CompanyEmail)> CreateEmployeeAsync(string personalEmail, DateOnly? joinDate = null)
        {
            var request = Setup.BuildRequest(adminId, personalEmail, joinDate);
            var response = await staff.AddEmployeeAsync(request, CancellationToken.None);
            Assert.Equal("SENT", response.EmailDelivery);
            UserId = response.UserId;
            return (response.UserId, response.CompanyEmail);
        }

        public async Task<LoginResponse> Login(string identifier, string password)
        {
            context.ChangeTracker.Clear();
            var result = await login.Handle(new LoginCommand { Identifier = identifier, Password = password }, default);
            return result as OkObjectResult is { Value: LoginResponse response }
                ? response
                : throw new InvalidCredentialsFailure();
        }

        public async Task<IActionResult> LoginRaw(string identifier, string password)
        {
            context.ChangeTracker.Clear();
            return await login.Handle(new LoginCommand { Identifier = identifier, Password = password }, default);
        }

        public async Task<IActionResult> ChangePassword(Guid userId, string currentPassword)
        {
            context.ChangeTracker.Clear();
            var handler = new ChangePasswordHandler(users, new PasswordHasherService(),
                new ChangePasswordValidator(), new HttpContextAccessor { HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "synthetic-test"))
                } });
            return await handler.Handle(new ChangePasswordCommand { CurrentPassword = currentPassword,
                NewPassword = NewPassword, ConfirmNewPassword = NewPassword }, default);
        }

        public Task<AddEmployeeResponse> AdminReset(Guid userId, string personalEmail, Guid? actorId = null) =>
            Reset(context, userId, personalEmail, actorId ?? adminId);

        private async Task<AddEmployeeResponse> Reset(SmartPoolDbContext db, Guid userId, string personalEmail, Guid actorId)
        {
            db.ChangeTracker.Clear();
            return await staff.ResetStaffPasswordAsync(userId, actorId, personalEmail, default);
        }

        public async Task<User> CreateCustomerAsync()
        {
            var role = await context.Roles.FirstAsync(item => item.Name == "CUSTOMER" && item.IsDeleted != true);
            var user = new User { Id = Guid.NewGuid(), Username = "synthetic-customer", Email = "customer@example.test",
                Status = "Active", IsDeleted = false, RoleId = role.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            user.PasswordHash = new PasswordHasherService().HashPassword(user, "Customer123!");
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }
    }

    private static class Setup
    {
        private static readonly Guid ShiftId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        public static async Task<SmartPoolDbContext> CreateContextAsync()
        {
            var options = await IntegrationTestDatabase.CreateOptionsAsync();
            var context = new SmartPoolDbContext(options);
            if (!await context.Shifts.AnyAsync(item => item.Id == ShiftId))
            {
                context.Shifts.Add(new Shift { Id = ShiftId, Name = "Auth acceptance shift",
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0), IsActive = true });
                await context.SaveChangesAsync();
            }
            return context;
        }

        public static async Task<Harness> WireAsync(SmartPoolDbContext context, RecordingMail mail, TimeProvider? clock = null)
        {
            var timeProvider = clock ?? TimeProvider.System;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "synthetic-acceptance-secret-key-0123456789abcdef",
                ["JwtSettings:Issuer"] = "synthetic-issuer",
                ["JwtSettings:Audience"] = "synthetic-audience",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7"
            }).Build();
            var hasher = new PasswordHasherService();
            var contractAccess = new StaffContractAccessPolicy(context, timeProvider);
            var accessTokens = new AccessTokenService(configuration, context, contractAccess);
            var refreshTokens = new RefreshTokenService(configuration, accessTokens, context, contractAccess);
            var users = new UserOperations(context);
            var login = new LoginHandler(users, hasher, accessTokens, refreshTokens, new LoginValidator(), contractAccess);
            var changePassword = new ChangePasswordHandler(users, hasher, new ChangePasswordValidator(), new HttpContextAccessor());
            var services = new StaffOnboardingServices(Options.Create(new StaffOnboardingOptions
            {
                EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                IdentityKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                FrontendOrigin = "https://example.test"
            }), hasher, mail);
            var mapper = new MapperConfiguration(config => config.AddProfile<StaffMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
            var staff = new StaffOperations(context, mapper, timeProvider,
                Options.Create(new SmartPool.Application.Features.ManageStaffs.AttendanceQr.StaffAttendanceQrSettings()),
                services, new EphemeralDataProtectionProvider(), new CompanyEmailGenerator(context), new TempPasswordGenerator());
            var adminId = (await context.Users.FirstAsync(item => item.Role!.Name == "ADMIN" && item.IsDeleted != true)).Id;
            return new Harness(context, mail, staff, users, accessTokens, refreshTokens, login, changePassword, adminId);
        }

        public static AddEmployeeCommand BuildRequest(Guid actorId, string personalEmail, DateOnly? requestedJoinDate = null)
        {
            var joinDate = requestedJoinDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).Date);
            return new AddEmployeeCommand
            {
                ActorId = actorId, FullName = "Trần Thị Auth", Gender = "FEMALE",
                DateOfBirth = new DateOnly(1996, 3, 3),
                Phone = "09" + RandomNumberGenerator.GetInt32(10000000, 99999999),
                PersonalEmail = personalEmail,
                CccdNumber = "00" + RandomNumberGenerator.GetInt32(10000, 99999) + RandomNumberGenerator.GetInt32(10000, 99999),
                PermanentAddress = "2 Synthetic Street", JoinDate = joinDate, TargetRole = "SALES",
                Contract = new ContractTerms("FULL_TIME", joinDate, joinDate.AddDays(10),
                    Enumerable.Range(1, 7).Select(day => new WeeklyPatternDay(day, [ShiftId])).ToList())
            };
        }
    }

    private sealed class RecordingMail : IEmailService
    {
        public bool FailDelivery { get; set; }
        public string LastRecipient { get; private set; } = "";
        public string LastTemporaryPassword { get; private set; } = "";
        public List<string> Recipients { get; } = [];
        public string Subject { get; private set; } = "";
        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Recipients.Add(to); Subject = subject; LastRecipient = to;
            var match = Regex.Match(htmlBody, "Mật khẩu tạm thời: ([A-Za-z0-9]+)");
            if (match.Success) LastTemporaryPassword = match.Groups[1].Value;
            if (FailDelivery) throw new InvalidOperationException("Synthetic delivery failure");
            return Task.CompletedTask;
        }
        public Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public void Reset() { Recipients.Clear(); Subject = ""; }
    }


    private sealed class InvalidCredentialsFailure : Exception;
}





using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.Authentication.Commands.PasswordReset;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Infrastructure.Services;

namespace SmartPool.IntegrationTests.AuthenticationAccess;

public sealed class OtpAuthenticationApiTests
{
    [Fact, Trait("TestCaseId", "AA-REG-01")]
    public async Task RegistrationStartStoresChallengeWithoutCreatingAccount()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var (email, pending) = await StartRegistration(f);
        Assert.NotEqual(Guid.Empty, pending.RegistrationId);
        Assert.True(pending.OtpExpiresAtUtc < pending.RegistrationExpiresAtUtc);
        Assert.Single(f.Email.Messages, m => m.To == email);
        await using var db = f.CreateDb();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));
    }

    [Fact, Trait("TestCaseId", "AA-REG-02")]
    public async Task ValidRegistrationOtpCreatesOneCustomerAndReplayDoesNotDuplicate()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var (email, pending) = await StartRegistration(f);
        var otp = f.Email.LatestOtp(email);
        var payload = new { registrationId = pending.RegistrationId, otp };
        Assert.Equal(HttpStatusCode.Created,
            (await f.Client.PostAsJsonAsync("/api/auth/register/verify", payload)).StatusCode);
        await using var db = f.CreateDb();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.NotEqual(AuthenticationAccessFixture.Password, user.PasswordHash);
        Assert.NotNull(await db.UserProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id));
        Assert.Equal("CUSTOMER", await db.Roles.Where(r => r.Id == user.RoleId).Select(r => r.Name).SingleAsync());
        Assert.Equal(HttpStatusCode.Conflict,
            (await f.Client.PostAsJsonAsync("/api/auth/register/verify", payload)).StatusCode);
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == email));
    }

    [Fact, Trait("TestCaseId", "AA-REG-03")]
    public async Task RegistrationRejectsInvalidAndDuplicateInputs()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsJsonAsync("/api/auth/register",
            new { email = "invalid", phone = "123", password = "short" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsJsonAsync("/api/auth/register",
            new { email = f.Customer.Email, phone = "0399999999", password = AuthenticationAccessFixture.Password })).StatusCode);
        await using var db = f.CreateDb();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == f.Customer.Email));
    }

    [Fact, Trait("TestCaseId", "AA-REG-04")]
    public async Task RegistrationOtpExpiryAndAttemptLimitRejectVerification()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var (email, pending) = await StartRegistration(f, "attempts");
        var wrong = f.Email.LatestOtp(email) == "000000" ? "111111" : "000000";
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await f.Client.PostAsJsonAsync("/api/auth/register/verify",
                new { registrationId = pending.RegistrationId, otp = wrong });
            Assert.Equal(attempt == 5 ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest,
                response.StatusCode);
        }
        var (expiredEmail, expired) = await StartRegistration(f, "expired");
        await f.SetChallengeFieldAsync("registration", expired.RegistrationId, "otpExpires", "0");
        Assert.Equal(HttpStatusCode.Gone, (await f.Client.PostAsJsonAsync("/api/auth/register/verify",
            new { registrationId = expired.RegistrationId, otp = f.Email.LatestOtp(expiredEmail) })).StatusCode);
        await using var db = f.CreateDb();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email || u.Email == expiredEmail));
    }

    [Fact, Trait("TestCaseId", "AA-REG-05")]
    public async Task ResendingRegistrationOtpEnforcesCooldownAndReplacesOldCode()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var (email, pending) = await StartRegistration(f);
        var oldOtp = f.Email.LatestOtp(email);
        var payload = new { registrationId = pending.RegistrationId };
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await f.Client.PostAsJsonAsync("/api/auth/register/resend", payload)).StatusCode);
        await f.SetChallengeFieldAsync("registration", pending.RegistrationId, "nextResend", "0");
        var resent = await f.Client.PostAsJsonAsync("/api/auth/register/resend", payload);
        Assert.Equal(HttpStatusCode.Accepted, resent.StatusCode);
        var details = (await resent.Content.ReadFromJsonAsync<ResendRegistrationResponse>())!;
        Assert.Equal(pending.RegistrationExpiresAtUtc, details.RegistrationExpiresAtUtc);
        var newOtp = f.Email.LatestOtp(email);
        Assert.NotEqual(oldOtp, newOtp);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsJsonAsync("/api/auth/register/verify",
            new { registrationId = pending.RegistrationId, otp = oldOtp })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await f.Client.PostAsJsonAsync("/api/auth/register/verify",
            new { registrationId = pending.RegistrationId, otp = newOtp })).StatusCode);
    }

    [Fact, Trait("TestCaseId", "AA-RST-01")]
    public async Task ResetRequestDoesNotRevealWhetherAccountExists()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var known = await StartReset(f, f.Editor.Email!);
        var unknownEmail = $"unknown_{f.Suffix}@example.test";
        var unknown = await StartReset(f, unknownEmail);
        Assert.NotEqual(Guid.Empty, known.RecoveryId);
        Assert.NotEqual(Guid.Empty, unknown.RecoveryId);
        Assert.Single(f.Email.Messages, m => m.To == f.Editor.Email);
        Assert.DoesNotContain(f.Email.Messages, m => m.To == unknownEmail);
        await using var db = f.CreateDb();
        Assert.Equal(f.Editor.PasswordHash,
            await db.Users.Where(u => u.Id == f.Editor.Id).Select(u => u.PasswordHash).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "AA-RST-02")]
    public async Task ValidResetOtpIssuesSingleUseGrantWithoutChangingPassword()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var pending = await StartReset(f, f.Editor.Email!);
        var payload = new { recoveryId = pending.RecoveryId, otp = f.Email.LatestOtp(f.Editor.Email!) };
        var response = await f.Client.PostAsJsonAsync("/api/auth/password-reset/verify", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var verified = (await response.Content.ReadFromJsonAsync<VerifyPasswordResetResponse>())!;
        Assert.NotEmpty(verified.ResetToken);
        Assert.Equal(HttpStatusCode.Conflict,
            (await f.Client.PostAsJsonAsync("/api/auth/password-reset/verify", payload)).StatusCode);
        await using var db = f.CreateDb();
        Assert.Equal(f.Editor.PasswordHash,
            await db.Users.Where(u => u.Id == f.Editor.Id).Select(u => u.PasswordHash).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "AA-RST-03")]
    public async Task CompletingResetInvalidatesOldCredentialsAndTokens()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var oldLogin = await f.LoginAsync(f.Editor);
        var pending = await StartReset(f, f.Editor.Email!);
        var grant = await VerifyReset(f, pending.RecoveryId, f.Editor.Email!);
        var payload = new { recoveryId = pending.RecoveryId, resetToken = grant.ResetToken,
            newPassword = "ResetAuth456!", confirmPassword = "ResetAuth456!" };
        Assert.Equal(HttpStatusCode.NoContent,
            (await f.Client.PostAsJsonAsync("/api/auth/password-reset/complete", payload)).StatusCode);
        Assert.NotEqual(HttpStatusCode.NoContent,
            (await f.Client.PostAsJsonAsync("/api/auth/password-reset/complete", payload)).StatusCode);
        f.Use(oldLogin.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        f.Use(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = oldLogin.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/login",
            new { identifier = f.Editor.Username, password = AuthenticationAccessFixture.Password })).StatusCode);
        await f.LoginAsync(f.Editor, password: "ResetAuth456!");
    }

    [Fact, Trait("TestCaseId", "AA-RST-04")]
    public async Task ResetOtpExpiryCooldownAndAttemptLimitAreEnforced()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var pending = await StartReset(f, f.Editor.Email!);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await f.Client.PostAsJsonAsync(
            "/api/auth/password-reset/resend", new { recoveryId = pending.RecoveryId })).StatusCode);
        var wrong = f.Email.LatestOtp(f.Editor.Email!) == "000000" ? "111111" : "000000";
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await f.Client.PostAsJsonAsync("/api/auth/password-reset/verify",
                new { recoveryId = pending.RecoveryId, otp = wrong });
            Assert.Equal(attempt == 5 ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest,
                response.StatusCode);
        }
        var second = await StartReset(f, f.Editor.Email!);
        await f.SetChallengeFieldAsync("reset", second.RecoveryId, "otpExpires", "0");
        Assert.Equal(HttpStatusCode.Gone, (await f.Client.PostAsJsonAsync("/api/auth/password-reset/verify",
            new { recoveryId = second.RecoveryId, otp = f.Email.LatestOtp(f.Editor.Email!) })).StatusCode);
    }

    [Fact, Trait("TestCaseId", "AA-RST-05")]
    public async Task ResetCannotOverwritePasswordChangedAfterGrantIssued()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var pending = await StartReset(f, f.Editor.Email!);
        var grant = await VerifyReset(f, pending.RecoveryId, f.Editor.Email!);
        var newHash = new PasswordHasherService().HashPassword(f.Editor, "LaterAuth789!");
        await using (var db = f.CreateDb())
            await db.Users.Where(u => u.Id == f.Editor.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordHash, newHash));
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsJsonAsync(
            "/api/auth/password-reset/complete", new { recoveryId = pending.RecoveryId,
                resetToken = grant.ResetToken, newPassword = "ResetAuth456!",
                confirmPassword = "ResetAuth456!" })).StatusCode);
        await using var check = f.CreateDb();
        Assert.Equal(newHash, await check.Users.Where(u => u.Id == f.Editor.Id)
            .Select(u => u.PasswordHash).SingleAsync());
    }

    private static async Task<(string Email, RegisterPendingResponse Pending)> StartRegistration(
        AuthenticationAccessFixture fixture, string tag = "normal")
    {
        var email = $"register_{tag}_{fixture.Suffix}@example.test";
        var phone = $"03{Math.Abs((fixture.Suffix + tag).GetHashCode() % 100000000):D8}";
        var response = await fixture.Client.PostAsJsonAsync("/api/auth/register",
            new { email, phone, password = AuthenticationAccessFixture.Password });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var pending = (await response.Content.ReadFromJsonAsync<RegisterPendingResponse>())!;
        fixture.TrackChallenge("registration", pending.RegistrationId);
        return (email, pending);
    }

    private static async Task<RequestPasswordResetResponse> StartReset(
        AuthenticationAccessFixture fixture, string email)
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/auth/password-reset/request", new { email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var pending = (await response.Content.ReadFromJsonAsync<RequestPasswordResetResponse>())!;
        fixture.TrackChallenge("reset", pending.RecoveryId);
        fixture.TrackChallenge("reset-grant", pending.RecoveryId);
        return pending;
    }

    private static async Task<VerifyPasswordResetResponse> VerifyReset(AuthenticationAccessFixture fixture,
        Guid id, string email)
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/auth/password-reset/verify",
            new { recoveryId = id, otp = fixture.Email.LatestOtp(email) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VerifyPasswordResetResponse>())!;
    }
}

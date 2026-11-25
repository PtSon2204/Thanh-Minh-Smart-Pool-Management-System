using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.Authentication.Commands.Login;
using SmartPool.Application.Features.Authentication.Commands.Refresh;

namespace SmartPool.IntegrationTests.AuthenticationAccess;

public sealed class AuthenticationSessionApiTests
{
    [Fact, Trait("TestCaseId", "AA-LOG-01")]
    public async Task LoginAcceptsUsernameEmailAndPhoneAndIssuesUsableTokens()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        foreach (var identifier in new[] { f.Editor.Username!, f.Editor.Email!, f.Editor.Phone! })
        {
            var login = await f.LoginAsync(f.Editor, identifier);
            Assert.Equal(f.Editor.Id, login.Id);
            Assert.NotEmpty(login.AccessToken);
            Assert.NotEmpty(login.RefreshToken);
            Assert.Equal(f.Editor.Role!.Name, login.Role);
            f.Use(login.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
            f.Use(null);
        }
    }

    [Fact, Trait("TestCaseId", "AA-LOG-02")]
    public async Task WrongPasswordAndUnavailableAccountsCannotLogin()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/login",
            new { identifier = f.Customer.Username, password = "wrong" })).StatusCode);
        await using (var db = f.CreateDb())
            await db.Users.Where(u => u.Id == f.Customer.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, "LOCKED"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/login",
            new { identifier = f.Customer.Username, password = AuthenticationAccessFixture.Password })).StatusCode);
        await using (var db = f.CreateDb())
            await db.Users.Where(u => u.Id == f.Customer.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.IsDeleted, true));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/login",
            new { identifier = f.Customer.Username, password = AuthenticationAccessFixture.Password })).StatusCode);
        await using var check = f.CreateDb();
        Assert.Empty(await check.RefreshTokens.Where(t => t.UserId == f.Customer.Id).ToListAsync());
    }

    [Fact, Trait("TestCaseId", "AA-LOG-03")]
    public async Task InvalidLoginFieldsReturnValidationErrors()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        foreach (var payload in new object[]
        {
            new { identifier = " ", password = AuthenticationAccessFixture.Password },
            new { identifier = new string('a', 151), password = AuthenticationAccessFixture.Password },
            new { identifier = f.Customer.Username!, password = "" }
        })
        {
            var response = await f.Client.PostAsJsonAsync("/api/auth/login", payload);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(body.RootElement.TryGetProperty("errors", out _));
        }
        await using var db = f.CreateDb();
        Assert.Empty(await db.RefreshTokens.Where(t => t.UserId == f.Customer.Id).ToListAsync());
    }

    [Fact, Trait("TestCaseId", "AA-LOG-04")]
    public async Task InvalidAccessTokenCannotReadProtectedProfile()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var token = (await f.LoginAsync(f.Editor)).AccessToken;
        var signatureStart = token.LastIndexOf('.') + 1;
        f.Use(token[..signatureStart] + (token[signatureStart] == 'A' ? 'B' : 'A') + token[(signatureStart + 1)..]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        f.Use("invalid.jwt.token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        f.Use(MakeToken("wrong-audience", DateTime.UtcNow.AddMinutes(5)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        f.Use(MakeToken("auth-access-tests", DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);

        static string MakeToken(string audience, DateTime expires)
        {
            var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                "authentication-access-integration-test-signing-secret-2026")), SecurityAlgorithms.HmacSha256);
            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = "auth-access-tests", Audience = audience, IssuedAt = DateTime.UtcNow.AddMinutes(-3),
                NotBefore = DateTime.UtcNow.AddMinutes(-2), Expires = expires,
                SigningCredentials = credentials
            });
        }
    }

    [Fact, Trait("TestCaseId", "AA-SES-01")]
    public async Task RefreshRotatesAndOldTokenCannotBeUsedAgain()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var initial = await f.LoginAsync(f.Editor);
        var response = await f.Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = initial.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rotated = (await response.Content.ReadFromJsonAsync<RefreshResponse>())!;
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);
        f.Use(rotated.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = initial.RefreshToken })).StatusCode);
    }

    [Fact, Trait("TestCaseId", "AA-SES-02")]
    public async Task ConcurrentRefreshRequestsCannotBothRotateSameToken()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var initial = await f.LoginAsync(f.Editor);
        var requests = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            f.Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = initial.RefreshToken })));
        Assert.Single(requests, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(requests, response => response.StatusCode == HttpStatusCode.Unauthorized);
        foreach (var response in requests) response.Dispose();
    }

    [Fact, Trait("TestCaseId", "AA-SES-03")]
    public async Task LogoutRevokesRefreshToken()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var login = await f.LoginAsync(f.Editor);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = login.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = login.RefreshToken })).StatusCode);
    }

    [Fact, Trait("TestCaseId", "AA-SES-04")]
    public async Task RefreshAndLogoutValidateTokensAndAccountAvailability()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = new string('a', 43) })).StatusCode);
        var login = await f.LoginAsync(f.Editor);
        await using (var db = f.CreateDb())
            await db.Users.Where(u => u.Id == f.Editor.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, "LOCKED"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = login.RefreshToken })).StatusCode);
    }

    [Fact, Trait("TestCaseId", "AA-PWD-01")]
    public async Task ChangePasswordInvalidatesOldCredentialsAndTokens()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var login = await f.LoginAsync(f.Editor);
        f.Use(login.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Client.PutAsJsonAsync("/api/auth/change-password",
            new { currentPassword = AuthenticationAccessFixture.Password, newPassword = "NewAuth456!",
                confirmNewPassword = "NewAuth456!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        f.Use(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = login.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/login",
            new { identifier = f.Editor.Username, password = AuthenticationAccessFixture.Password })).StatusCode);
        await f.LoginAsync(f.Editor, password: "NewAuth456!");
    }

    [Fact, Trait("TestCaseId", "AA-PWD-02")]
    public async Task FailedChangePasswordLeavesHashAndSessionIntact()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var login = await f.LoginAsync(f.Editor);
        f.Use(null);
        var payload = new { currentPassword = "wrong", newPassword = "NewAuth456!", confirmNewPassword = "NewAuth456!" };
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await f.Client.PutAsJsonAsync("/api/auth/change-password", payload)).StatusCode);
        f.Use(login.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await f.Client.PutAsJsonAsync("/api/auth/change-password", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PutAsJsonAsync("/api/auth/change-password",
            new { currentPassword = AuthenticationAccessFixture.Password, newPassword = "short",
                confirmNewPassword = "different" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        f.Use(null);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = login.RefreshToken })).StatusCode);
    }
}

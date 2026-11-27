using System.Net;
using System.Net.Http.Json;
using SmartPool.Application.Features.Authentication.Commands.Login;

namespace SmartPool.IntegrationTests.AuthenticationAccess;

public sealed class GoogleAuthenticationApiTests
{
    [Fact, Trait("TestCaseId", "AA-GOO-01")]
    public async Task LinkedGoogleAccountReceivesUsableTokenPair()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var user = await f.AddGoogleUserAsync();
        var response = await SendGoogle(f, "/api/auth/google", new { credential = "valid-google-token" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(user.Id, login.Id);
        Assert.NotEmpty(login.RefreshToken);
        f.Use(login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/users")).StatusCode);
    }

    [Fact, Trait("TestCaseId", "AA-GOO-02")]
    public async Task GoogleRegistrationCreatesCustomerAndIssuesTokenPair()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var response = await SendGoogle(f, "/api/auth/google/register", new
        {
            credential = "valid-google-token", username = $"qa_google_{f.Suffix}",
            phone = $"07{Math.Abs(f.Suffix.GetHashCode() % 100000000):D8}", fullName = "Google Test"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal("CUSTOMER", login.Role);
        Assert.NotEmpty(login.RefreshToken);
        await using var db = f.CreateDb();
        var saved = Assert.Single(db.Users.Where(u => u.Email == f.GoogleEmail));
        Assert.Equal(saved.Id, login.Id);
        Assert.NotNull(db.UserProfiles.SingleOrDefault(p => p.UserId == saved.Id));
        f.Use(login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/users")).StatusCode);
    }

    [Fact, Trait("TestCaseId", "AA-GOO-03")]
    public async Task GoogleEndpointsRejectMissingHeaderInvalidCredentialAndEmailConflict()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsJsonAsync("/api/auth/google",
            new { credential = "valid-google-token" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsJsonAsync("/api/auth/google/register",
            new { credential = "valid-google-token", username = "google_test", phone = "0912345678",
                fullName = "Test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendGoogle(f, "/api/auth/google",
            new { credential = "invalid" })).StatusCode);
        await f.AddGoogleUserAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await SendGoogle(f, "/api/auth/google/register", new
        {
            credential = "valid-google-token", username = $"another_{f.Suffix}",
            phone = "0912345678", fullName = "Other"
        })).StatusCode);
    }

    private static async Task<HttpResponseMessage> SendGoogle(AuthenticationAccessFixture fixture,
        string path, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await fixture.Client.SendAsync(request);
    }
}

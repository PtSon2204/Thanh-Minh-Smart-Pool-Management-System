using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace SmartPool.IntegrationTests.AuthenticationAccess;

public sealed class AuthorizationApiTests
{
    [Fact, Trait("TestCaseId", "AA-ACL-01")]
    public async Task UsersEndpointDistinguishesAnonymousCustomerAndAdmin()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/users")).StatusCode);
        f.Use((await f.LoginAsync(f.Customer)).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/users")).StatusCode);
        f.Use((await f.LoginAsync(f.Admin)).AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/users")).StatusCode);
    }

    [Fact, Trait("TestCaseId", "AA-ACL-02")]
    public async Task PermissionGrantAndRevocationApplyToExistingJwt()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var token = (await f.LoginAsync(f.Reader)).AccessToken;
        f.Use(token);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        var payload = new { fullName = "Updated Reader", address = "Test Street", dateOfBirth = "2000-01-01" };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await f.Client.PutAsJsonAsync("/api/profiles/me", payload)).StatusCode);
        await SetPermissions("""{"profiles":["read","update"]}""");
        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.PutAsJsonAsync("/api/profiles/me", payload)).StatusCode);
        await SetPermissions("""{"profiles":["read"]}""");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await f.Client.PutAsJsonAsync("/api/profiles/me", payload)).StatusCode);

        async Task SetPermissions(string value)
        {
            await using var db = f.CreateDb();
            await db.Roles.Where(r => r.Id == f.Reader.RoleId)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.Permissions, value));
        }
    }

    [Fact, Trait("TestCaseId", "AA-ACL-03")]
    public async Task RolePermissionApiRequiresAdminAndPersistsChange()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/roles")).StatusCode);
        f.Use((await f.LoginAsync(f.Customer)).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/roles")).StatusCode);
        f.Use((await f.LoginAsync(f.Admin)).AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.GetAsync("/api/roles/permissions/catalog")).StatusCode);

        await using var db = f.CreateDb();
        var sales = await db.Roles.AsNoTracking().SingleAsync(r => r.Name == "SALES" && r.IsDeleted != true);
        var original = sales.Permissions;
        try
        {
            await db.Roles.Where(r => r.Id == sales.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.Permissions, "{}"));
            var result = await f.Client.PatchAsJsonAsync($"/api/roles/{sales.Id}/permissions",
                new { module = "profiles", action = "read", enabled = true });
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains("read", (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == sales.Id)).Permissions!);
        }
        finally
        {
            await db.Roles.Where(r => r.Id == sales.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.Permissions, original));
        }
    }

    [Fact, Trait("TestCaseId", "AA-ACL-04")]
    public async Task RoleStatusAndDeletionChangesInvalidateOldJwt()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var login = await f.LoginAsync(f.Editor);
        f.Use(login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        await using (var db = f.CreateDb())
            await db.Users.Where(u => u.Id == f.Editor.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.RoleId, f.Customer.RoleId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        var refreshed = await f.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        using (var body = System.Text.Json.JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync()))
            Assert.Equal("CUSTOMER", body.RootElement.GetProperty("role").GetString());

        var replacement = await f.LoginAsync(f.Editor);
        f.Use(replacement.AccessToken);
        await using (var db = f.CreateDb())
            await db.Users.Where(u => u.Id == f.Editor.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, "LOCKED"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        await using (var db = f.CreateDb())
            await db.Users.Where(u => u.Id == f.Editor.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, "ACTIVE"));
        var finalLogin = await f.LoginAsync(f.Editor);
        f.Use(finalLogin.AccessToken);
        await using (var db = f.CreateDb())
            await db.Users.Where(u => u.Id == f.Editor.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.IsDeleted, true));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
    }

    [Fact, Trait("TestCaseId", "AA-ACL-05")]
    public async Task PoolAccessActionsRequireSeparatePermissions()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var token = (await f.LoginAsync(f.Reader)).AccessToken;
        f.Use(token);
        await using (var db = f.CreateDb())
            await db.Roles.Where(r => r.Id == f.Reader.RoleId)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.Permissions,
                    """{"ticket_lookup":["read"]}"""));
        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.PostAsJsonAsync("/api/pool-access/lookup", new { code = "UNKNOWN" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await f.Client.PostAsJsonAsync("/api/pool-access/entries", new { code = "UNKNOWN" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await f.Client.GetAsync("/api/pool-access/entries")).StatusCode);
        await using var check = f.CreateDb();
        Assert.Equal(0, await check.EntryLogs.CountAsync(log => log.OperatorId == f.Reader.Id));
    }

    [Fact, Trait("TestCaseId", "AA-ACL-06")]
    public async Task MalformedPermissionsAndMismatchedRoleFailClosed()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var token = (await f.LoginAsync(f.Reader)).AccessToken;
        f.Use(token);
        await using (var db = f.CreateDb())
            await db.Roles.Where(r => r.Id == f.Reader.RoleId)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.Permissions, "[]"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
        await using (var db = f.CreateDb())
            await db.Users.Where(u => u.Id == f.Reader.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.RoleId, f.Customer.RoleId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/profiles/me")).StatusCode);
    }
}

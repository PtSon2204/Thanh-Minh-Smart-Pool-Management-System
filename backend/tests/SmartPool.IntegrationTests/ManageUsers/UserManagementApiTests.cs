using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;
using SmartPool.Application.Features.ManageUsers.Commands.CreateUser;
using SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;
using SmartPool.Application.Features.ManageUsers.Queries.GetUserById;
using SmartPool.Application.Features.ManageUsers.Queries.GetUsers;
using SmartPool.IntegrationTests.AuthenticationAccess;

namespace SmartPool.IntegrationTests.ManageUsers;

public sealed class UserManagementApiTests
{
    [Fact, Trait("TestCaseId", "UM-AUTH-01")]
    public async Task EveryUserManagementEndpointRequiresAdmin()
    {
        await using var f = await AuthenticationAccessFixture.CreateAsync();
        var id = f.Customer.Id;
        foreach (var expected in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
        {
            if (expected == HttpStatusCode.Forbidden)
                f.Use((await f.LoginAsync(f.Customer)).AccessToken);

            Assert.Equal(expected, (await f.Client.GetAsync("/api/users")).StatusCode);
            Assert.Equal(expected, (await f.Client.GetAsync($"/api/users/{id}")).StatusCode);
            Assert.Equal(expected, (await f.Client.PostAsJsonAsync("/api/users", NewUser(f))).StatusCode);
            Assert.Equal(expected, (await f.Client.PutAsJsonAsync($"/api/users/{id}", Update(f))).StatusCode);
            Assert.Equal(expected, (await f.Client.PatchAsJsonAsync($"/api/users/{id}/role", new { role = "SALES" })).StatusCode);
            Assert.Equal(expected, (await f.Client.PatchAsJsonAsync($"/api/users/{id}/status", new { status = "LOCKED" })).StatusCode);
        }

        await using var db = f.CreateDb();
        Assert.Equal("Active", await db.Users.Where(u => u.Id == id).Select(u => u.Status).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "UM-CREATE-01")]
    public async Task CreateUserPersistsNormalizedAccountAndProfileAndEmailsUsablePassword()
    {
        await using var f = await AdminFixtureAsync();
        var payload = NewUser(f);
        var response = await f.Client.PostAsJsonAsync("/api/users", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CreateUserResponse>())!;
        Assert.Equal(payload.Username.ToLowerInvariant(), created.Username);
        Assert.Equal(payload.Email.ToLowerInvariant(), created.Email);
        Assert.Equal("CUSTOMER", created.Role);
        Assert.Equal("ACTIVE", created.Status);

        await using (var db = f.CreateDb())
        {
            var user = await db.Users.Include(u => u.UserProfile).SingleAsync(u => u.Id == created.Id);
            Assert.Equal(created.Email, user.Email);
            Assert.Equal(payload.FullName, user.UserProfile!.FullName);
            Assert.Equal(payload.Address, user.UserProfile.Address);
            Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
        }

        var email = Assert.Single(f.Email.Messages, m => m.To == created.Email);
        var password = Regex.Match(email.Body, @"Tm1![A-F0-9]{32}").Value;
        Assert.NotEmpty(password);
        f.Use(null);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsJsonAsync("/api/auth/login",
            new { identifier = created.Username, password })).StatusCode);
    }

    [Fact, Trait("TestCaseId", "UM-CREATE-02")]
    public async Task CreateUserRejectsInvalidInputWithoutWritingOrSendingEmail()
    {
        await using var f = await AdminFixtureAsync();
        var payload = NewUser(f) with { Username = "x", Role = "UNKNOWN" };
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsJsonAsync("/api/users", payload)).StatusCode);
        Assert.Empty(f.Email.Messages);
        await using var db = f.CreateDb();
        Assert.False(await db.Users.AnyAsync(u => u.Email == payload.Email));
    }

    [Fact, Trait("TestCaseId", "UM-CREATE-03")]
    public async Task CreateUserRejectsDuplicateUsernameEmailAndPhone()
    {
        await using var f = await AdminFixtureAsync();
        var payload = NewUser(f);
        Assert.Equal(HttpStatusCode.Created, (await f.Client.PostAsJsonAsync("/api/users", payload)).StatusCode);
        foreach (var duplicate in new[]
        {
            payload with { Email = $"other_{f.Suffix}@example.test", Phone = Phone(f, 2) },
            payload with { Username = $"other_{f.Suffix}", Phone = Phone(f, 2) },
            payload with { Username = $"other_{f.Suffix}", Email = $"other_{f.Suffix}@example.test" }
        })
            Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsJsonAsync("/api/users", duplicate)).StatusCode);
        Assert.Single(f.Email.Messages);
    }

    [Fact, Trait("TestCaseId", "UM-READ-01")]
    public async Task AdminCanReadUserDetailAndSearchFilteredPagedList()
    {
        await using var f = await AdminFixtureAsync();
        var create = await f.Client.PostAsJsonAsync("/api/users", NewUser(f));
        var created = (await create.Content.ReadFromJsonAsync<CreateUserResponse>())!;

        var detail = await f.Client.GetFromJsonAsync<GetUserByIdResponse>($"/api/users/{created.Id}");
        Assert.NotNull(detail);
        Assert.Equal(created.Id, detail.Id);
        Assert.Equal(created.FullName, detail.FullName);
        Assert.Equal(created.Address, detail.Address);

        var list = await f.Client.GetFromJsonAsync<PagedResponse<GetUsersResponse>>(
            $"/api/users?searchTerm={created.Username}&role=CUSTOMER&status=ACTIVE&pageIndex=1&pageSize=1");
        Assert.NotNull(list);
        Assert.Equal(1, list.TotalCount);
        Assert.Equal(created.Id, Assert.Single(list.Items).Id);
        Assert.Equal(1, list.TotalPages);
        var secondPage = await f.Client.GetFromJsonAsync<PagedResponse<GetUsersResponse>>(
            $"/api/users?searchTerm={created.Username}&pageIndex=2&pageSize=1");
        Assert.NotNull(secondPage);
        Assert.Empty(secondPage.Items);
    }

    [Fact, Trait("TestCaseId", "UM-READ-02")]
    public async Task MissingUsersAndInvalidListFiltersReturnExpectedErrors()
    {
        await using var f = await AdminFixtureAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
        foreach (var query in new[] { "pageIndex=0", "pageSize=101", "role=UNKNOWN", "status=DISABLED" })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.GetAsync($"/api/users?{query}")).StatusCode);
    }

    [Fact, Trait("TestCaseId", "UM-UPDATE-01")]
    public async Task UpdateUserNormalizesContactAndPersistsProfile()
    {
        await using var f = await AdminFixtureAsync();
        var payload = Update(f);
        var response = await f.Client.PutAsJsonAsync($"/api/users/{f.Customer.Id}", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<UpdateUserResponse>())!;
        Assert.Equal(payload.Email.ToLowerInvariant(), updated.Email);
        Assert.Equal(payload.FullName, updated.FullName);
        Assert.Equal(payload.DateOfBirth, updated.DateOfBirth);
        await using var db = f.CreateDb();
        var user = await db.Users.Include(u => u.UserProfile).SingleAsync(u => u.Id == f.Customer.Id);
        Assert.Equal(payload.Email.ToLowerInvariant(), user.Email);
        Assert.Equal(payload.Phone, user.Phone);
        Assert.Equal(payload.Address, user.UserProfile!.Address);
    }

    [Fact, Trait("TestCaseId", "UM-UPDATE-02")]
    public async Task UpdateUserRejectsConflictValidationAndMissingTargetWithoutMutation()
    {
        await using var f = await AdminFixtureAsync();
        var url = $"/api/users/{f.Customer.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PutAsJsonAsync(url,
            Update(f) with { Email = f.Admin.Email! })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PutAsJsonAsync(url,
            Update(f) with { Phone = f.Admin.Phone! })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PutAsJsonAsync(url,
            Update(f) with { Email = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", Update(f))).StatusCode);
        await using var db = f.CreateDb();
        Assert.Equal(f.Customer.Email, await db.Users.Where(u => u.Id == f.Customer.Id).Select(u => u.Email).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "UM-ROLE-01")]
    public async Task AdminCanChangeAnotherUsersRoleButCannotDemoteSelf()
    {
        await using var f = await AdminFixtureAsync();
        var response = await f.Client.PatchAsJsonAsync($"/api/users/{f.Customer.Id}/role", new { role = "SALES" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("SALES", (await response.Content.ReadFromJsonAsync<ChangeUserRoleResponse>())!.Role);
        await using var db = f.CreateDb();
        Assert.Equal("SALES", await db.Users.Where(u => u.Id == f.Customer.Id).Select(u => u.Role!.Name).SingleAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PatchAsJsonAsync($"/api/users/{f.Admin.Id}/role",
            new { role = "CUSTOMER" })).StatusCode);
        Assert.Equal("ADMIN", await db.Users.Where(u => u.Id == f.Admin.Id).Select(u => u.Role!.Name).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "UM-ROLE-02")]
    public async Task RoleChangeRejectsInvalidUnavailableAndMissingRoles()
    {
        await using var f = await AdminFixtureAsync();
        var url = $"/api/users/{f.Customer.Id}/role";
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PatchAsJsonAsync(url, new { role = "UNKNOWN" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PatchAsJsonAsync(url, new { role = "COACH" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.PatchAsJsonAsync($"/api/users/{Guid.NewGuid()}/role",
            new { role = "CUSTOMER" })).StatusCode);
    }

    [Fact, Trait("TestCaseId", "UM-STATUS-01")]
    public async Task LockAndUnlockUserChangesLoginAvailability()
    {
        await using var f = await AdminFixtureAsync();
        var url = $"/api/users/{f.Customer.Id}/status";
        var locked = await f.Client.PatchAsJsonAsync(url, new { status = "LOCKED" });
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Equal("LOCKED", (await locked.Content.ReadFromJsonAsync<ChangeUserStatusResponse>())!.Status);
        f.Use(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync("/api/auth/login",
            new { identifier = f.Customer.Username, password = AuthenticationAccessFixture.Password })).StatusCode);
        f.Use((await f.LoginAsync(f.Admin)).AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PatchAsJsonAsync(url, new { status = "ACTIVE" })).StatusCode);
        f.Use(null);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsJsonAsync("/api/auth/login",
            new { identifier = f.Customer.Username, password = AuthenticationAccessFixture.Password })).StatusCode);
    }

    [Fact, Trait("TestCaseId", "UM-STATUS-02")]
    public async Task StatusChangeRejectsInvalidValueMissingUserAndSelfLock()
    {
        await using var f = await AdminFixtureAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PatchAsJsonAsync($"/api/users/{f.Customer.Id}/status",
            new { status = "DISABLED" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.PatchAsJsonAsync($"/api/users/{Guid.NewGuid()}/status",
            new { status = "LOCKED" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PatchAsJsonAsync($"/api/users/{f.Admin.Id}/status",
            new { status = "LOCKED" })).StatusCode);
        await using var db = f.CreateDb();
        Assert.Equal("Active", await db.Users.Where(u => u.Id == f.Admin.Id).Select(u => u.Status).SingleAsync());
    }

    private static async Task<AuthenticationAccessFixture> AdminFixtureAsync()
    {
        var fixture = await AuthenticationAccessFixture.CreateAsync();
        fixture.Use((await fixture.LoginAsync(fixture.Admin)).AccessToken);
        return fixture;
    }

    private static NewUserPayload NewUser(AuthenticationAccessFixture f) => new(
        $"Qa_Manage_{f.Suffix}", $"Manage_{f.Suffix}@example.test", Phone(f, 1),
        "Managed User", new DateOnly(2000, 1, 2), "Pool Street", "CUSTOMER");

    private static UpdatePayload Update(AuthenticationAccessFixture f) => new(
        $"Updated_{f.Suffix}@example.test", Phone(f, 3), "Updated Customer",
        new DateOnly(1998, 2, 3), "New Street");

    private static string Phone(AuthenticationAccessFixture f, int offset) =>
        $"08{(Convert.ToUInt32(f.Suffix[..8], 16) + (uint)offset) % 100000000:D8}";

    private sealed record NewUserPayload(string Username, string Email, string Phone, string FullName,
        DateOnly DateOfBirth, string Address, string Role);

    private sealed record UpdatePayload(string Email, string Phone, string FullName,
        DateOnly DateOfBirth, string Address);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.Profiles;

public sealed class ProfileManagementApiTests
{
    [Fact]
    [Trait("TestCaseId", "PF-PRO-01")]
    public async Task CustomerCanReadAndUpdateOnlyTheirOwnProfile()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Use("CUSTOMER");

        var read = await fixture.Client.GetAsync("/api/profiles/me");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using (var profile = JsonDocument.Parse(await read.Content.ReadAsStringAsync()))
        {
            Assert.Equal(fixture.CustomerId, profile.RootElement.GetProperty("id").GetGuid());
            Assert.Equal("Customer Before", profile.RootElement.GetProperty("fullName").GetString());
            Assert.Equal(fixture.CustomerEmail, profile.RootElement.GetProperty("email").GetString());
        }

        var update = await fixture.Client.PutAsJsonAsync("/api/profiles/me", new
        {
            userId = fixture.OtherCustomerId,
            fullName = "Customer After",
            address = "Pool Street 1",
            dateOfBirth = "2000-02-03"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        await using var db = fixture.CreateContext();
        var customerProfile = await db.UserProfiles.AsNoTracking().SingleAsync(x => x.UserId == fixture.CustomerId);
        var otherProfile = await db.UserProfiles.AsNoTracking().SingleAsync(x => x.UserId == fixture.OtherCustomerId);
        Assert.Equal("Customer After", customerProfile.FullName);
        Assert.Equal("Pool Street 1", customerProfile.Address);
        Assert.Equal(new DateOnly(2000, 2, 3), customerProfile.DateOfBirth);
        Assert.Equal("Other Customer", otherProfile.FullName);

        var reread = await fixture.Client.GetFromJsonAsync<ProfileResponse>("/api/profiles/me");
        Assert.NotNull(reread);
        Assert.Equal("Customer After", reread.FullName);
    }

    [Fact]
    [Trait("TestCaseId", "PF-PRO-02")]
    public async Task ProfileEndpointsEnforceAuthenticationPermissionAndValidation()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/profiles/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client.PutAsJsonAsync("/api/profiles/me", new { fullName = "Name" })).StatusCode);

        fixture.Use("PROFILE_READER");
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/profiles/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.Client.PutAsJsonAsync("/api/profiles/me", new { fullName = "No Update" })).StatusCode);

        fixture.Use("CUSTOMER");
        var invalidName = await fixture.Client.PutAsJsonAsync("/api/profiles/me", new
        {
            fullName = " ", address = "Valid", dateOfBirth = "2000-01-01"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidName.StatusCode);
        var futureBirthDate = await fixture.Client.PutAsJsonAsync("/api/profiles/me", new
        {
            fullName = "Invalid Date", address = "Valid", dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        });
        Assert.Equal(HttpStatusCode.BadRequest, futureBirthDate.StatusCode);

        await using var db = fixture.CreateContext();
        Assert.Equal("Customer Before", await db.UserProfiles.Where(x => x.UserId == fixture.CustomerId)
            .Select(x => x.FullName).SingleAsync());
    }

    [Fact]
    [Trait("TestCaseId", "PF-TK-01")]
    [Trait("TestCaseId", "PF-QR-01")]
    public async Task CustomerCanViewOnlyTheirTicketsAndReadTicketQrCode()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticketTypeId = await fixture.AddTicketTypeAsync();
        var ownTicketId = await fixture.AddTicketAsync(ticketTypeId, fixture.CustomerId, "621037");
        await fixture.AddTicketAsync(ticketTypeId, fixture.OtherCustomerId, "621038");

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/tickets/me")).StatusCode);
        fixture.Use("CUSTOMER");
        var response = await fixture.Client.GetAsync("/api/tickets/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tickets = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var own = Assert.Single(tickets.RootElement.EnumerateArray());
        Assert.Equal(ownTicketId, own.GetProperty("id").GetGuid());
        Assert.Equal("621037", own.GetProperty("qrCode").GetString());
        Assert.Equal("QA Profile Ticket", own.GetProperty("ticketName").GetString());
    }

    [Fact]
    [Trait("TestCaseId", "PF-QR-02")]
    [Trait("TestCaseId", "PF-QR-03")]
    [Trait("TestCaseId", "PF-QR-04")]
    [Trait("TestCaseId", "PF-QR-05")]
    public async Task TicketQrUpdatePersistsValidCodeAndRejectsInvalidOrUnownedChanges()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticketTypeId = await fixture.AddTicketTypeAsync();
        var ownTicketId = await fixture.AddTicketAsync(ticketTypeId, fixture.CustomerId, "731001");
        var otherTicketId = await fixture.AddTicketAsync(ticketTypeId, fixture.OtherCustomerId, "731002");
        var inactiveTicketId = await fixture.AddTicketAsync(ticketTypeId, fixture.CustomerId, "731003", status: "USED");

        fixture.Use("CUSTOMER");
        var changed = await fixture.Client.PutAsJsonAsync($"/api/tickets/{ownTicketId}/code", new { code = "123456" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal("123456", await fixture.ReadTicketCodeAsync(ownTicketId));

        foreach (var invalidCode in new[] { "12", "1234567", "ABC123", "" })
            Assert.Equal(HttpStatusCode.BadRequest,
                (await fixture.Client.PutAsJsonAsync($"/api/tickets/{ownTicketId}/code", new { code = invalidCode })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.Client.PutAsJsonAsync($"/api/tickets/{ownTicketId}/code", new { code = "731002" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.Client.PutAsJsonAsync($"/api/tickets/{inactiveTicketId}/code", new { code = "123457" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.Client.PutAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/code", new { code = "123458" })).StatusCode);

        fixture.Use("OTHER_CUSTOMER");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.Client.PutAsJsonAsync($"/api/tickets/{ownTicketId}/code", new { code = "123459" })).StatusCode);
        Assert.Equal("123456", await fixture.ReadTicketCodeAsync(ownTicketId));
        Assert.Equal("731002", await fixture.ReadTicketCodeAsync(otherTicketId));

        fixture.Use("PROFILE_READER");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.Client.PutAsJsonAsync($"/api/tickets/{ownTicketId}/code", new { code = "123460" })).StatusCode);
        Assert.Equal("123456", await fixture.ReadTicketCodeAsync(ownTicketId));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DbContextOptions<SmartPoolDbContext> options;
        private readonly WebApplication app;
        private readonly Dictionary<string, User> users;
        private readonly Dictionary<string, string> tokens;
        private readonly HashSet<Guid> ticketTypeIds = [];
        private readonly HashSet<Guid> ticketIds = [];
        private readonly HashSet<Guid> userIds;
        private readonly HashSet<Guid> roleIds;

        private Fixture(DbContextOptions<SmartPoolDbContext> options, WebApplication app,
            Dictionary<string, User> users, HashSet<Guid> roleIds)
        {
            this.options = options;
            this.app = app;
            this.users = users;
            this.roleIds = roleIds;
            userIds = users.Values.Select(user => user.Id).ToHashSet();
            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using var scope = app.Services.CreateScope();
            var tokenService = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
            tokens = users.ToDictionary(pair => pair.Key,
                pair => tokenService.CreateToken(pair.Value, pair.Value.Role!.Name).Token);
        }

        public HttpClient Client { get; }
        public Guid CustomerId => users["CUSTOMER"].Id;
        public Guid OtherCustomerId => users["OTHER_CUSTOMER"].Id;
        public string CustomerEmail => users["CUSTOMER"].Email;

        public static async Task<Fixture> CreateAsync()
        {
            var options = await IntegrationTestDatabase.CreateOptionsAsync();
            await using var db = new SmartPoolDbContext(options);
            var suffix = Guid.NewGuid().ToString("N");
            var definitions = new Dictionary<string, string>
            {
                ["CUSTOMER"] = """{"profiles":["read","update"],"customer_tickets":["read","update"]}""",
                ["OTHER_CUSTOMER"] = """{"profiles":["read","update"],"customer_tickets":["read","update"]}""",
                ["PROFILE_READER"] = """{"profiles":["read"],"customer_tickets":["read"]}"""
            };
            var roles = definitions.ToDictionary(pair => pair.Key, pair => new Role
            {
                Id = Guid.NewGuid(), Name = $"QA_PROFILE_{pair.Key}_{suffix}", Permissions = pair.Value, IsDeleted = false
            });
            var phoneSuffix = new Dictionary<string, int> { ["CUSTOMER"] = 1, ["OTHER_CUSTOMER"] = 2, ["PROFILE_READER"] = 3 };
            var users = roles.ToDictionary(pair => pair.Key, pair => new User
            {
                Id = Guid.NewGuid(), Role = pair.Value, Username = $"qa_profile_{pair.Key}_{suffix}",
                Email = $"{pair.Key.ToLowerInvariant()}_{suffix}@example.test", Phone = $"09{suffix[..8]}{phoneSuffix[pair.Key]}",
                PasswordHash = "test-only", Status = "ACTIVE", IsDeleted = false
            });
            db.Roles.AddRange(roles.Values);
            db.Users.AddRange(users.Values);
            db.UserProfiles.AddRange(
                new UserProfile { UserId = users["CUSTOMER"].Id, FullName = "Customer Before", Address = "Old Address", DateOfBirth = new DateOnly(1999, 1, 1) },
                new UserProfile { UserId = users["OTHER_CUSTOMER"].Id, FullName = "Other Customer", Address = "Other Address" },
                new UserProfile { UserId = users["PROFILE_READER"].Id, FullName = "Profile Reader", Address = "Reader Address" });
            await db.SaveChangesAsync();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = IntegrationTestDatabase.GetConnectionString(),
                ["ConnectionStrings:Redis"] = IntegrationTestSettings.RedisConnection,
                ["JwtSettings:SecretKey"] = "profile-management-integration-tests-signing-key-at-least-32-bytes",
                ["JwtSettings:Issuer"] = "profile-management-tests",
                ["JwtSettings:Audience"] = "profile-management-tests",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "5",
                ["CloudinarySettings:ApiKey"] = "test", ["CloudinarySettings:ApiSecret"] = "test",
                ["CloudinarySettings:CloudName"] = "test"
            });
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddInfrastructureServices(builder.Configuration);
            builder.Services.AddApiAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(ProfilesController).Assembly);
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            try
            {
                await app.StartAsync();
                return new Fixture(options, app, users, roles.Values.Select(role => role.Id).ToHashSet());
            }
            catch
            {
                await app.DisposeAsync();
                await using var cleanup = new SmartPoolDbContext(options);
                cleanup.UserProfiles.RemoveRange(await cleanup.UserProfiles.Where(profile => users.Values.Select(user => user.Id).Contains(profile.UserId)).ToListAsync());
                cleanup.Users.RemoveRange(await cleanup.Users.Where(user => users.Values.Select(item => item.Id).Contains(user.Id)).ToListAsync());
                cleanup.Roles.RemoveRange(await cleanup.Roles.Where(role => roles.Values.Select(item => item.Id).Contains(role.Id)).ToListAsync());
                await cleanup.SaveChangesAsync();
                throw;
            }
        }

        public SmartPoolDbContext CreateContext() => new(options);
        public void Use(string userKey) => Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens[userKey]);

        public async Task<Guid> AddTicketTypeAsync()
        {
            var id = Guid.NewGuid();
            ticketTypeIds.Add(id);
            await using var db = CreateContext();
            db.TicketTypes.Add(new TicketType
            {
                Id = id, Name = "QA Profile Ticket", TicketCategory = "VE_LUOT", Price = 10000,
                TotalEntries = 1, IsActive = true, IsDeleted = false
            });
            await db.SaveChangesAsync();
            return id;
        }

        public async Task<Guid> AddTicketAsync(Guid typeId, Guid ownerId, string code, string status = "ACTIVE")
        {
            var id = Guid.NewGuid();
            ticketIds.Add(id);
            await using var db = CreateContext();
            db.Tickets.Add(new Ticket
            {
                Id = id, TicketTypeId = typeId, UserId = ownerId, QrCode = code,
                Status = status, RemainingEntries = 1, IsDeleted = false, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return id;
        }

        public async Task<string?> ReadTicketCodeAsync(Guid ticketId)
        {
            await using var db = CreateContext();
            return await db.Tickets.AsNoTracking().Where(ticket => ticket.Id == ticketId)
                .Select(ticket => ticket.QrCode).SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
            await using var db = CreateContext();
            await db.EntryLogs.Where(log => log.TicketId != null && ticketIds.Contains(log.TicketId.Value)).ExecuteDeleteAsync();
            await db.Tickets.Where(ticket => ticketIds.Contains(ticket.Id)).ExecuteDeleteAsync();
            await db.TicketTypes.Where(type => ticketTypeIds.Contains(type.Id)).ExecuteDeleteAsync();
            await db.UserProfiles.Where(profile => userIds.Contains(profile.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(user => userIds.Contains(user.Id)).ExecuteDeleteAsync();
            await db.Roles.Where(role => roleIds.Contains(role.Id)).ExecuteDeleteAsync();
        }
    }

    private sealed class ProfileResponse
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
    }
}

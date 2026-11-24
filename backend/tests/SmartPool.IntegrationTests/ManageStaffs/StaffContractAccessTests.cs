using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.API.Controllers;
using SmartPool.Application;
using SmartPool.Application.Features.Authentication.Commands.Login;
using SmartPool.Application.Features.Authentication.Commands.Refresh;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Constants;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using SmartPool.Infrastructure.Persistence.Repositories.Users;
using SmartPool.Infrastructure.Services;

namespace SmartPool.IntegrationTests.ManageStaffs;

public sealed class StaffContractAccessTests
{
    [Fact]
    public async Task VietnamContractBoundaryRejectsLoginRefreshAndIssuedJwtWithoutUnlockingManualBlocks()
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        await using var db = new SmartPoolDbContext(options);
        var end = DateOnly.FromDateTime(DateTime.UtcNow);
        var clock = new ContractClock(new DateTimeOffset(end.ToDateTime(new TimeOnly(16, 59, 59)), TimeSpan.Zero));
        var suffix = Guid.NewGuid().ToString("N");
        var roles = new[] { "ADMIN", "SALES", "LIFEGUARD", "COACH", "CUSTOMER" }
            .ToDictionary(name => name, name => new Role { Id = Guid.NewGuid(), Name = name, Permissions = "{}", IsDeleted = false });
        var users = new Dictionary<string, User>();
        var hasher = new PasswordHasherService();
        foreach (var name in new[] { "SALES", "LIFEGUARD", "COACH", "ADMIN", "LEGACY", "CUSTOMER" })
        {
            var user = new User { Id = Guid.NewGuid(), Role = roles[name == "LEGACY" ? "SALES" : name],
                Username = $"contract_{name}_{suffix}", Email = $"{name.ToLowerInvariant()}{suffix}@gmail.com",
                Status = "Active", IsDeleted = false, IsCoach = name == "CUSTOMER" };
            user.PasswordHash = name == "CUSTOMER" ? null : hasher.HashPassword(user, "ContractTest123!");
            users.Add(name, user);
        }
        db.Users.AddRange(users.Values);
        foreach (var name in users.Keys.Where(name => name != "CUSTOMER"))
            db.Employees.Add(new Employee { UserId = users[name].Id, Status = "Working", JoinDate = end.AddDays(-10) });
        foreach (var name in new[] { "SALES", "LIFEGUARD", "COACH", "ADMIN" })
            AddPattern(name, end.AddDays(-10), end, end.AddDays(-10), 1);
        await db.SaveChangesAsync();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = IntegrationTestDatabase.GetConnectionString(),
            ["JwtSettings:SecretKey"] = new string('t', 48), ["JwtSettings:Issuer"] = "contract-tests",
            ["JwtSettings:Audience"] = "contract-tests", ["JwtSettings:AccessTokenExpirationMinutes"] = "10",
            ["JwtSettings:RefreshTokenExpirationDays"] = "1",
            ["CloudinarySettings:ApiKey"] = "test", ["CloudinarySettings:ApiSecret"] = "test",
            ["CloudinarySettings:CloudName"] = "test"
        });
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<IGoogleIdTokenVerifier>(new GoogleVerifier(users["CUSTOMER"].Email!));
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/contract-probe", () => "authorized").RequireAuthorization();
        try
        {
            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var issued = await Login("SALES");
            var originalRefresh = issued.RefreshToken;
            Assert.Equal(HttpStatusCode.OK, await Probe(issued.AccessToken));
            var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = originalRefresh });
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            var rotated = (await refresh.Content.ReadFromJsonAsync<RefreshResponse>())!;
            var lifeguard = await Login("LIFEGUARD");
            var coach = await Login("COACH");
            var admin = await Login("ADMIN");
            var legacy = await Login("LEGACY");

            AddPattern("SALES", end.AddDays(3), end.AddDays(30), end.AddDays(3), 2);
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.OK, await Probe(issued.AccessToken));
            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(issued.AccessToken));
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(lifeguard.AccessToken));
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(coach.AccessToken));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new { identifier = users["SALES"].Username, password = "ContractTest123!" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = rotated.RefreshToken })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, await Probe(admin.AccessToken));
            Assert.Equal(HttpStatusCode.OK, await Probe(legacy.AccessToken));

            await Sync();
            var inactive = await ReadEmployee("SALES");
            Assert.Equal("Inactive", inactive.Status);
            Assert.NotNull(inactive.ContractInactiveSinceUtc);
            Assert.Equal("LOCKED", await ReadUserStatus("SALES"));
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(issued.AccessToken));
            clock.UtcNow = clock.UtcNow.AddDays(2);
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(issued.AccessToken));
            await Sync();
            Assert.Equal("Working", (await ReadEmployee("SALES")).Status);
            Assert.Null((await ReadEmployee("SALES")).ContractInactiveSinceUtc);
            Assert.Equal("ACTIVE", await ReadUserStatus("SALES"));
            Assert.Equal(HttpStatusCode.OK, await Probe(issued.AccessToken));
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login",
                new { identifier = users["SALES"].Username, password = "ContractTest123!" })).StatusCode);

            await db.Employees.Where(item => item.UserId == users["SALES"].Id).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, "Inactive").SetProperty(item => item.ContractInactiveSinceUtc, clock.UtcNow.UtcDateTime));
            using (var scope = app.Services.CreateScope())
            {
                var operations = new StaffOperations(scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>(),
                    scope.ServiceProvider.GetRequiredService<IMapper>(), clock, Options.Create(new StaffAttendanceQrSettings()));
                await operations.UpdateStaffAsync(new UpdateStaffCommand { UserId = users["SALES"].Id, Status = "Inactive" }, default);
            }
            await Sync();
            Assert.Equal("Inactive", (await ReadEmployee("SALES")).Status);
            Assert.Null((await ReadEmployee("SALES")).ContractInactiveSinceUtc);
            Assert.Equal("LOCKED", await ReadUserStatus("SALES"));
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(issued.AccessToken));

            AddPattern("COACH", end.AddDays(3), end.AddDays(30), end.AddDays(3), 2);
            await db.SaveChangesAsync();
            using (var scope = app.Services.CreateScope())
                await new UserOperations(scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>())
                    .ChangeUserStatusAsync(users["COACH"].Id, users["ADMIN"].Id, SmartPool.Domain.Enums.UserStatusEnum.LOCKED, default);
            await Sync();
            Assert.Equal("Inactive", (await ReadEmployee("COACH")).Status);
            Assert.Null((await ReadEmployee("COACH")).ContractInactiveSinceUtc);
            Assert.Equal("LOCKED", await ReadUserStatus("COACH"));
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(coach.AccessToken));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new { identifier = users["COACH"].Username, password = "ContractTest123!" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = coach.RefreshToken })).StatusCode);
            Assert.Equal("Working", (await ReadEmployee("ADMIN")).Status);
            Assert.Equal("Working", (await ReadEmployee("LEGACY")).Status);
            Assert.Equal("Active", await ReadUserStatus("ADMIN"));

            await db.Employees.Where(item => item.UserId == users["LEGACY"].Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Inactive"));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new { identifier = users["LEGACY"].Username, password = "ContractTest123!" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(legacy.AccessToken));
            Assert.Equal(HttpStatusCode.OK, await Probe(admin.AccessToken));

            using (var scope = app.Services.CreateScope())
            {
                var operations = new StaffOperations(scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>(),
                    scope.ServiceProvider.GetRequiredService<IMapper>(), clock, Options.Create(new StaffAttendanceQrSettings()));
                await operations.UpdateStaffAsync(new UpdateStaffCommand
                    { UserId = users["LIFEGUARD"].Id, Status = "Working" }, default);
            }
            await Sync();
            Assert.Equal("Working", (await ReadEmployee("LIFEGUARD")).Status);
            Assert.True((await ReadEmployee("LIFEGUARD")).ManualAccessEnabled);
            Assert.Equal("ACTIVE", await ReadUserStatus("LIFEGUARD"));
            var manuallyReactivated = await Login("LIFEGUARD");
            Assert.Equal(HttpStatusCode.OK, await Probe(manuallyReactivated.AccessToken));
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = manuallyReactivated.RefreshToken })).StatusCode);

            using (var scope = app.Services.CreateScope())
            {
                var operations = new StaffOperations(scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>(),
                    scope.ServiceProvider.GetRequiredService<IMapper>(), clock, Options.Create(new StaffAttendanceQrSettings()));
                await operations.UpdateStaffAsync(new UpdateStaffCommand
                    { UserId = users["LIFEGUARD"].Id, Status = "Inactive" }, default);
            }
            await Sync();
            Assert.False((await ReadEmployee("LIFEGUARD")).ManualAccessEnabled);
            Assert.Equal("LOCKED", await ReadUserStatus("LIFEGUARD"));
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(manuallyReactivated.AccessToken));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new { identifier = users["LIFEGUARD"].Username, password = "ContractTest123!" })).StatusCode);

            using (var scope = app.Services.CreateScope())
                await new UserOperations(scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>())
                    .ChangeUserStatusAsync(users["LIFEGUARD"].Id, users["ADMIN"].Id, SmartPool.Domain.Enums.UserStatusEnum.ACTIVE, default);
            await Sync();
            Assert.Equal("Working", (await ReadEmployee("LIFEGUARD")).Status);
            Assert.True((await ReadEmployee("LIFEGUARD")).ManualAccessEnabled);
            await Login("LIFEGUARD");
            using (var scope = app.Services.CreateScope())
                await new UserOperations(scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>())
                    .ChangeUserStatusAsync(users["LIFEGUARD"].Id, users["ADMIN"].Id, SmartPool.Domain.Enums.UserStatusEnum.LOCKED, default);
            Assert.Equal("Inactive", (await ReadEmployee("LIFEGUARD")).Status);
            Assert.False((await ReadEmployee("LIFEGUARD")).ManualAccessEnabled);
            Assert.Equal(HttpStatusCode.Unauthorized, await Probe(manuallyReactivated.AccessToken));

            using var googleRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/google")
                { Content = JsonContent.Create(new { credential = "synthetic" }) };
            googleRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
            var google = await client.SendAsync(googleRequest);
            Assert.Equal(HttpStatusCode.OK, google.StatusCode);
            var customer = (await google.Content.ReadFromJsonAsync<LoginResponse>())!;
            Assert.Equal(HttpStatusCode.OK, await Probe(customer.AccessToken));
            Assert.Contains("COACH", customer.Roles);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = customer.RefreshToken })).StatusCode);

            async Task<LoginResponse> Login(string name)
            {
                var response = await client.PostAsJsonAsync("/api/auth/login", new
                    { identifier = users[name].Username, password = "ContractTest123!" });
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
            }
            async Task<HttpStatusCode> Probe(string token)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/contract-probe");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.SendAsync(request);
                return response.StatusCode;
            }
        }
        finally
        {
            await app.StopAsync();
            var ids = users.Values.Select(item => item.Id).ToArray();
            await db.RefreshTokens.Where(item => ids.Contains(item.UserId)).ExecuteDeleteAsync();
            await db.StaffScheduleChanges.Where(item => ids.Contains(item.EmployeeId)).ExecuteDeleteAsync();
            await db.Employees.Where(item => ids.Contains(item.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
            var roleIds = roles.Values.Select(item => item.Id).ToArray();
            await db.Roles.Where(item => roleIds.Contains(item.Id)).ExecuteDeleteAsync();
        }

        void AddPattern(string name, DateOnly start, DateOnly last, DateOnly effective, long version) =>
            db.StaffScheduleChanges.Add(new StaffScheduleChange { EmployeeId = users[name].Id, Kind = "Pattern",
                AppliedVersion = version, EffectiveFrom = effective, CreatedBy = users["ADMIN"].Id,
                CreatedAtUtc = clock.UtcNow.UtcDateTime,
                ContractJson = JsonSerializer.Serialize(new ContractTerms("FULL_TIME", start, last,
                    Enumerable.Range(1, 7).Select(day => new WeeklyPatternDay(day,
                        day == 1 ? [FixedShifts.All[0].Id] : [])).ToArray()), new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        async Task<Employee> ReadEmployee(string name) => await db.Employees.AsNoTracking().SingleAsync(item => item.UserId == users[name].Id);
        async Task<string?> ReadUserStatus(string name) => await db.Users.AsNoTracking()
            .Where(item => item.Id == users[name].Id).Select(item => item.Status).SingleAsync();
        async Task Sync()
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<StaffContractLifecycle>().SynchronizeAsync();
        }
    }

    private sealed class ContractClock(DateTimeOffset instant) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = instant;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class GoogleVerifier(string email) : IGoogleIdTokenVerifier
    {
        public Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken) =>
            Task.FromResult<GoogleIdentity?>(new("synthetic-customer", email, true));
    }
}

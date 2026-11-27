using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.ManageSwimClasses;

internal sealed class SwimClassFixture : IAsyncDisposable
{
    private readonly DbContextOptions<SmartPoolDbContext> options;
    private readonly WebApplication app;
    private readonly Guid coachRoleId;
    private readonly Dictionary<Guid, string> tokens;
    private readonly Guid[] userIds;

    private SwimClassFixture(DbContextOptions<SmartPoolDbContext> options, WebApplication app,
        Guid coachRoleId, User owner, User otherCoach, User student, User ordinaryCustomer)
    {
        this.options = options;
        this.app = app;
        this.coachRoleId = coachRoleId;
        Owner = owner;
        OtherCoach = otherCoach;
        Student = student;
        OrdinaryCustomer = ordinaryCustomer;
        userIds = [owner.Id, otherCoach.Id, student.Id, ordinaryCustomer.Id];
        Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var scope = app.Services.CreateScope();
        var accessTokens = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
        tokens = new Dictionary<Guid, string>
        {
            [owner.Id] = accessTokens.CreateToken(owner, "COACH").Token,
            [otherCoach.Id] = accessTokens.CreateToken(otherCoach, "COACH").Token,
            [student.Id] = accessTokens.CreateToken(student, "CUSTOMER", ["CUSTOMER", "COACH"]).Token,
            [ordinaryCustomer.Id] = accessTokens.CreateToken(ordinaryCustomer, "CUSTOMER").Token
        };
    }

    public HttpClient Client { get; }
    public User Owner { get; }
    public User OtherCoach { get; }
    public User Student { get; }
    public User OrdinaryCustomer { get; }
    public string Suffix { get; private set; } = string.Empty;
    public SmartPoolDbContext CreateDb() => new(options);

    public static async Task<SwimClassFixture> CreateAsync()
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        var suffix = Guid.NewGuid().ToString("N");
        User owner, other, student, ordinary;
        var coachRole = new Role
        {
            Id = Guid.NewGuid(), Name = "COACH", IsDeleted = false,
            Permissions = """{"swim_classes":["read","create","update"],"class_sessions":["create","update"],"class_registrations":["read","create","update"],"class_attendances":["read","create","update"]}"""
        };
        await using (var db = new SmartPoolDbContext(options))
        {
            var customerRole = await db.Roles.SingleAsync(r => r.Name == "CUSTOMER" && r.IsDeleted != true);
            owner = MakeUser("owner", coachRole, 1, true);
            other = MakeUser("other", coachRole, 2, true);
            student = MakeUser("student", customerRole, 3, true);
            ordinary = MakeUser("ordinary", customerRole, 4, false);
            db.Roles.Add(coachRole);
            db.Users.AddRange(owner, other, student, ordinary);
            db.UserProfiles.AddRange(new UserProfile { UserId = owner.Id, FullName = "Owner Coach" },
                new UserProfile { UserId = other.Id, FullName = "Other Coach" },
                new UserProfile { UserId = student.Id, FullName = "Student" },
                new UserProfile { UserId = ordinary.Id, FullName = "Ordinary Customer" });
            await db.SaveChangesAsync();
        }

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = IntegrationTestDatabase.GetConnectionString(),
            ["ConnectionStrings:Redis"] = IntegrationTestSettings.RedisConnection,
            ["JwtSettings:SecretKey"] = "swim-class-integration-test-signing-secret-at-least-32-bytes",
            ["JwtSettings:Issuer"] = "swim-class-tests",
            ["JwtSettings:Audience"] = "swim-class-tests",
            ["JwtSettings:AccessTokenExpirationMinutes"] = "10",
            ["CloudinarySettings:ApiKey"] = "test", ["CloudinarySettings:ApiSecret"] = "test",
            ["CloudinarySettings:CloudName"] = "test"
        });
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddApiAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(SwimClassesController).Assembly);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        try
        {
            await app.StartAsync();
            return new SwimClassFixture(options, app, coachRole.Id, owner, other, student, ordinary)
                { Suffix = suffix };
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        User MakeUser(string label, Role role, int offset, bool isCoach) => new()
        {
            Id = Guid.NewGuid(), Role = role, Username = $"qa_class_{label}_{suffix}",
            Email = $"{label}_{suffix}@example.test", Phone = $"08{(Convert.ToUInt32(suffix[..8],16)+(uint)offset)%100000000:D8}",
            PasswordHash = "test-only", Status = "ACTIVE", IsDeleted = false, IsCoach = isCoach
        };
    }

    public void Use(User? user) => Client.DefaultRequestHeaders.Authorization = user is null
        ? null : new AuthenticationHeaderValue("Bearer", tokens[user.Id]);

    public object NewClass(string? name = null, int capacity = 3) => new
    {
        name = name ?? $"QA Class {Suffix}", description = "Integration class", capacity, fee = 150000m,
        registrationStartAt = DateTimeOffset.UtcNow.AddDays(-1),
        registrationEndAt = DateTimeOffset.UtcNow.AddDays(2)
    };

    public async Task<CreateSwimClassResponse> CreateClassAsync()
    {
        Use(Owner);
        var response = await Client.PostAsJsonAsync("/api/swim-classes", NewClass());
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateSwimClassResponse>())!;
    }

    public static DateTimeOffset SessionAt(int days, int hour = 7) =>
        new DateTimeOffset(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date.AddDays(days)
            .AddHours(hour), TimeSpan.FromHours(7));

    public async Task<ClassSessionResponse> AddSessionAsync(Guid classId, int days = 7)
    {
        Use(Owner);
        var start = SessionAt(days);
        var response = await Client.PostAsJsonAsync($"/api/swim-classes/{classId}/sessions",
            new { sessions = new[] { new { startsAt = start, endsAt = start.AddHours(1) } } });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return Assert.Single((await response.Content.ReadFromJsonAsync<CreateClassSessionsResponse>())!.Sessions);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
        await using var db = CreateDb();
        var classIds = await db.SwimClasses.Where(c => userIds.Contains(c.CreatedByUserId)).Select(c => c.Id).ToListAsync();
        await db.ClassAttendances.Where(a => classIds.Contains(a.Session.ClassId)).ExecuteDeleteAsync();
        await db.ClassRegistrations.Where(r => classIds.Contains(r.ClassId)).ExecuteDeleteAsync();
        await db.ClassSessions.Where(s => classIds.Contains(s.ClassId)).ExecuteDeleteAsync();
        await db.SwimClasses.Where(c => classIds.Contains(c.Id)).ExecuteDeleteAsync();
        await db.Notifications.Where(n => userIds.Contains(n.UserId)).ExecuteDeleteAsync();
        await db.RefreshTokens.Where(t => userIds.Contains(t.UserId)).ExecuteDeleteAsync();
        await db.UserProfiles.Where(p => userIds.Contains(p.UserId)).ExecuteDeleteAsync();
        await db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync();
        await db.Roles.Where(r => r.Id == coachRoleId).ExecuteDeleteAsync();
    }
}

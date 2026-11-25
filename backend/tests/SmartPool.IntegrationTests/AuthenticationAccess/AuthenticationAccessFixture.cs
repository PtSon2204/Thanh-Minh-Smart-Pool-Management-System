using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
using SmartPool.Application.Features.Authentication.Commands.Login;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Services;
using StackExchange.Redis;
using Role = SmartPool.Domain.Entities.Role;

namespace SmartPool.IntegrationTests.AuthenticationAccess;

internal sealed class AuthenticationAccessFixture : IAsyncDisposable
{
    private readonly DbContextOptions<SmartPoolDbContext> options;
    private readonly WebApplication app;
    private readonly Guid readerRoleId;
    private readonly Guid editorRoleId;
    private readonly string suffix;
    private readonly List<RedisKey> challengeKeys = [];

    private AuthenticationAccessFixture(DbContextOptions<SmartPoolDbContext> options,
        WebApplication app, Guid readerRoleId, Guid editorRoleId, string suffix,
        CapturedEmailService email)
    {
        this.options = options;
        this.app = app;
        this.readerRoleId = readerRoleId;
        this.editorRoleId = editorRoleId;
        this.suffix = suffix;
        Email = email;
        Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    }

    public HttpClient Client { get; }
    public CapturedEmailService Email { get; }
    public string Suffix => suffix;
    public string GoogleEmail => $"google_{suffix}@gmail.com";
    public const string Password = "AuthTest123!";
    public User Admin { get; private set; } = null!;
    public User Customer { get; private set; } = null!;
    public User Reader { get; private set; } = null!;
    public User Editor { get; private set; } = null!;

    public SmartPoolDbContext CreateDb() => new(options);

    public static async Task<AuthenticationAccessFixture> CreateAsync()
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var email = new CapturedEmailService();
        Guid readerRoleId, editorRoleId;
        User admin, customer, reader, editor;
        await using (var db = new SmartPoolDbContext(options))
        {
            var adminRole = await db.Roles.SingleAsync(r => r.Name == "ADMIN" && r.IsDeleted != true);
            var customerRole = await db.Roles.SingleAsync(r => r.Name == "CUSTOMER" && r.IsDeleted != true);
            var readerRole = new Role { Id = Guid.NewGuid(), Name = $"QA_AUTH_READER_{suffix}",
                Permissions = """{"profiles":["read"]}""", IsDeleted = false };
            var editorRole = new Role { Id = Guid.NewGuid(), Name = $"QA_AUTH_EDITOR_{suffix}",
                Permissions = """{"profiles":["read","update"]}""", IsDeleted = false };
            readerRoleId = readerRole.Id;
            editorRoleId = editorRole.Id;
            db.Roles.AddRange(readerRole, editorRole);

            var passwordHasher = new PasswordHasherService();
            admin = MakeUser("admin", adminRole, 1);
            customer = MakeUser("customer", customerRole, 2);
            reader = MakeUser("reader", readerRole, 3);
            editor = MakeUser("editor", editorRole, 4);
            db.Users.AddRange(admin, customer, reader, editor);
            db.UserProfiles.AddRange(new UserProfile { UserId = customer.Id, FullName = "Test Customer" },
                new UserProfile { UserId = reader.Id, FullName = "Test Reader" },
                new UserProfile { UserId = editor.Id, FullName = "Test Editor" });
            await db.SaveChangesAsync();

            User MakeUser(string name, Role role, int offset)
            {
                var user = new User { Id = Guid.NewGuid(), Role = role,
                    Username = $"qa_auth_{name}_{suffix}", Email = $"{name}_{suffix}@example.test",
                    Phone = $"09{Math.Abs(suffix.GetHashCode() % 10000000):D7}{offset}",
                    Status = "Active", IsDeleted = false };
                user.PasswordHash = passwordHasher.HashPassword(user, Password);
                return user;
            }
        }

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = IntegrationTestDatabase.GetConnectionString(),
            ["ConnectionStrings:Redis"] = IntegrationTestSettings.RedisConnection,
            ["JwtSettings:SecretKey"] = "authentication-access-integration-test-signing-secret-2026",
            ["JwtSettings:Issuer"] = "auth-access-tests",
            ["JwtSettings:Audience"] = "auth-access-tests",
            ["JwtSettings:AccessTokenExpirationMinutes"] = "10",
            ["JwtSettings:RefreshTokenExpirationDays"] = "1",
            ["RegistrationOtp:HmacKey"] = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray()),
            ["PasswordReset:HmacKey"] = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 41)).ToArray()),
            ["RegistrationOtp:MaxStartsPerIpPerHour"] = "1000",
            ["PasswordReset:MaxRequestsPerIpPerHour"] = "1000",
            ["CloudinarySettings:ApiKey"] = "test", ["CloudinarySettings:ApiSecret"] = "test",
            ["CloudinarySettings:CloudName"] = "test"
        });
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.RemoveAll<IEmailService>();
        builder.Services.AddSingleton<IEmailService>(email);
        builder.Services.RemoveAll<IGoogleIdTokenVerifier>();
        builder.Services.AddSingleton<IGoogleIdTokenVerifier>(new StubGoogleVerifier($"google_{suffix}@gmail.com"));
        builder.Services.AddApiAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        try
        {
            await app.StartAsync();
            return new AuthenticationAccessFixture(options, app, readerRoleId, editorRoleId, suffix, email)
                { Admin = admin, Customer = customer, Reader = reader, Editor = editor };
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public async Task<LoginResponse> LoginAsync(User user, string? identifier = null, string password = Password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            identifier = identifier ?? user.Username, password
        });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    public void Use(string? token) => Client.DefaultRequestHeaders.Authorization = token is null
        ? null : new AuthenticationHeaderValue("Bearer", token);

    public async Task<User> AddGoogleUserAsync()
    {
        await using var db = CreateDb();
        var role = await db.Roles.SingleAsync(r => r.Name == "CUSTOMER" && r.IsDeleted != true);
        var user = new User { Id = Guid.NewGuid(), RoleId = role.Id,
            Username = $"qa_google_{suffix}", Email = GoogleEmail,
            Phone = $"08{Math.Abs(suffix.GetHashCode() % 100000000):D8}",
            Status = "Active", IsDeleted = false };
        db.Users.Add(user);
        db.UserProfiles.Add(new UserProfile { UserId = user.Id, FullName = "Google User" });
        await db.SaveChangesAsync();
        return user;
    }

    public async Task SetChallengeFieldAsync(string kind, Guid id, string field, string value)
    {
        var key = ChallengeKey(kind, id);
        using var scope = app.Services.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        await redis.HashSetAsync(key, field, value);
    }

    public void TrackChallenge(string kind, Guid id) => challengeKeys.Add(ChallengeKey(kind, id));

    private static RedisKey ChallengeKey(string kind, Guid id) => kind switch
    {
        "registration" => $"SmartPool:registration-otp:{{registration-otp}}:session:{id:N}",
        "reset" => $"SmartPool:password-reset:{{password-reset}}:session:{id:N}",
        "reset-grant" => $"SmartPool:password-reset:{{password-reset}}:grant:{id:N}",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.StopAsync();
        using (var scope = app.Services.CreateScope())
        {
            var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
            foreach (var key in challengeKeys) await redis.KeyDeleteAsync(key);
        }
        await app.DisposeAsync();
        await using var db = CreateDb();
        var ids = await db.Users.Where(u => u.Email != null && u.Email.Contains(suffix))
            .Select(u => u.Id).ToListAsync();
        await db.RefreshTokens.Where(t => ids.Contains(t.UserId)).ExecuteDeleteAsync();
        await db.Notifications.Where(n => ids.Contains(n.UserId) ||
            n.Content != null && n.Content.Contains(suffix)).ExecuteDeleteAsync();
        await db.UserProfiles.Where(p => ids.Contains(p.UserId)).ExecuteDeleteAsync();
        await db.Users.Where(u => ids.Contains(u.Id)).ExecuteDeleteAsync();
        await db.Roles.Where(r => r.Id == readerRoleId || r.Id == editorRoleId).ExecuteDeleteAsync();
    }
}

internal sealed class CapturedEmailService : IEmailService
{
    private readonly List<(string To, string Subject, string Body)> messages = [];
    public IReadOnlyList<(string To, string Subject, string Body)> Messages => messages;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        lock (messages) messages.Add((to, subject, htmlBody));
        return Task.CompletedTask;
    }

    public Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody,
        CancellationToken cancellationToken = default) => Task.WhenAll(recipients.Select(to =>
            SendAsync(to, subject, htmlBody, cancellationToken)));

    public string LatestOtp(string to)
    {
        var message = Messages.Last(m => m.To == to);
        var match = Regex.Match(message.Body, @"(?<!\d)\d{6}(?!\d)");
        Assert.True(match.Success, "The stub email should contain a six-digit OTP.");
        return match.Value;
    }
}

internal sealed class StubGoogleVerifier(string email) : IGoogleIdTokenVerifier
{
    public Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken) =>
        Task.FromResult(idToken == "valid-google-token"
            ? new GoogleIdentity("qa-google-subject", email, true) : null);
}

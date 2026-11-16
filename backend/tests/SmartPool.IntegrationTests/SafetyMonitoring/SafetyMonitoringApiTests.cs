using System.Diagnostics;
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
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    private readonly Dictionary<string, Guid> apiUserIds = new();
    private async Task<WebApplication> ApiAsync(bool native = false, bool realCloudinary = false, Uri? simulator = null, string? snapshotPython = null)
    {
        await using var db = new SmartPoolDbContext(options);
        var role = new Role { Id = Guid.NewGuid(), Name = "ADMIN", Permissions = "[]", IsDeleted = false };
        db.Roles.Add(role);
        var user = await db.Users.SingleAsync(x => x.Id == userId); user.RoleId = role.Id;
        await db.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = simulator == null ? null : "Testing" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        if (native) builder.Logging.AddConsole().AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>(
            (category, level) => (category == typeof(SmartPool.Infrastructure.SafetyMonitoring.LiveMonitoringWorker).FullName ||
                category == typeof(SmartPool.Infrastructure.SafetyMonitoring.CameraFrameSource).FullName) && level >= LogLevel.Warning);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = db.Database.GetConnectionString(),
            ["ConnectionStrings:Redis"] = "localhost:56379,abortConnect=false,connectTimeout=100,connectRetry=0,syncTimeout=100,asyncTimeout=100",
            ["JwtSettings:SecretKey"] = "safety-integration-tests-signing-key-at-least-32-bytes",
            ["JwtSettings:Issuer"] = "safety-tests", ["JwtSettings:Audience"] = "safety-tests", ["JwtSettings:AccessTokenExpirationMinutes"] = "5",
            ["CloudinarySettings:CloudName"] = "test", ["CloudinarySettings:ApiKey"] = "test", ["CloudinarySettings:ApiSecret"] = "test",
            ["SafetyMonitoring:Enabled"] = native.ToString(),
            ["SafetyMonitoring:PythonPath"] = snapshotPython ?? Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_PYTHON"),
            ["SafetyMonitoring:WorkerPath"] = Path.Combine(RepositoryRoot(), "backend", "ai", "worker.py"),
            ["SafetyMonitoring:ModelPath"] = Path.Combine(RepositoryRoot(), "model", "best.onnx"),
            ["SafetyMonitoring:FontPath"] = Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_FONT"),
            ["SafetyMonitoring:StoragePath"] = Path.Combine(RepositoryRoot(), "backend", ".test-runtime", "jobs", database),
            ["CameraSettings:FfmpegPath"] = Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")
        });
        builder.Services.AddApplicationServices(builder.Configuration);
        if (simulator != null) builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["CameraSettings:AllowedEndpoints:0:Host"] = simulator.DnsSafeHost,
            ["CameraSettings:AllowedEndpoints:0:Port"] = simulator.Port.ToString(),
            ["CameraSettings:AllowedEndpoints:0:Simulator"] = "true" });
        if (realCloudinary) builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["CloudinarySettings:CloudName"] = Environment.GetEnvironmentVariable("SMARTPOOL_CLOUDINARY_TEST_CLOUD_NAME"),
            ["CloudinarySettings:ApiKey"] = Environment.GetEnvironmentVariable("SMARTPOOL_CLOUDINARY_TEST_API_KEY"),
            ["CloudinarySettings:ApiSecret"] = Environment.GetEnvironmentVariable("SMARTPOOL_CLOUDINARY_TEST_API_SECRET") });
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApiAuthorization();
        builder.Services.AddCors(c => c.AddDefaultPolicy(p => p.SetIsOriginAllowed(o => o.StartsWith("http://127.0.0.1:", StringComparison.Ordinal))
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        builder.Services.AddControllers().AddApplicationPart(typeof(CamerasController).Assembly);
        var app = builder.Build();
        app.UseCors(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); app.MapHubs();
        await app.StartAsync(); return app;
    }
    private static string RepositoryRoot()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path != null && !File.Exists(Path.Combine(path.FullName, "README.md"))) path = path.Parent;
        return path?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
    private async Task<string> TokenAsync(WebApplication app, string roleName)
    {
        await using var db = new SmartPoolDbContext(options);
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Name == roleName);
        if (role == null) { role = new() { Id = Guid.NewGuid(), Name = roleName, IsDeleted = false, Permissions = "[]" }; db.Roles.Add(role); }
        var user = new User { Id = Guid.NewGuid(), RoleId = role.Id, Username = "api-" + Guid.NewGuid().ToString("N"), Status = "Active", IsDeleted = false };
        db.Users.Add(user); await db.SaveChangesAsync();
        apiUserIds[roleName] = user.Id;
        using var scope = app.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IAccessTokenService>().CreateToken(user, roleName).Token;
    }

    [Fact]
    public async Task HttpEndpoints_EnforceRoles_JwtAndQueryValidation()
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        foreach (var path in new[] { "/api/cameras", "/api/alerts" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "SALES"));
        foreach (var path in new[] { "/api/cameras", "/api/alerts" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "LIFEGUARD"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/cameras")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "ADMIN"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/cameras")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/cameras?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/alerts?status=invalid")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "Admin"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/alerts")).StatusCode);
        var uiScript = Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_UI_SCRIPT");
        if (!string.IsNullOrWhiteSpace(uiScript) && File.Exists(uiScript))
        {
            var adminToken = await TokenAsync(app, "ADMIN"); var guardToken = await TokenAsync(app, "LIFEGUARD");
            var run = new ProcessStartInfo("node", uiScript) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(run)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(new { apiBase = app.Urls.Single(), adminToken, guardToken,
                adminId = apiUserIds["ADMIN"], guardId = apiUserIds["LIFEGUARD"], root = RepositoryRoot() }));
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
            Assert.True(process.ExitCode == 0, (await stdout) + (await stderr));
        }
        await app.StopAsync();
    }

    [Fact]
    public async Task ThresholdsEndpoint_ReadsForOperators_WritesForAdmins_AndHotReloads()
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        const string path = "/api/thresholds/safety-monitoring";
        // Chưa đăng nhập: 401.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);

        // Operator (LIFEGUARD) đọc được, không sửa được (403); SALES bị cấm (403).
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "LIFEGUARD"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync(path, new { confidenceThreshold = 0.7, warningRatio = 0.5, criticalRatio = 0.9,
                recoveryRatio = 0.2, criticalHoldSeconds = 3, recoveryHoldSeconds = 5, cooldownSeconds = 15,
                windowSeconds = 5, maxObservationGapSeconds = 3, minObservations = 3 })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "SALES"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);

        // Admin sửa được: ghi DB (bảng đã có seed row sau migration) + version tăng.
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "ADMIN"));
        var before = await client.GetFromJsonAsync<JsonDocument>(path);
        var saved = await client.PutAsJsonAsync(path, new
        {
            confidenceThreshold = 0.75,
            warningRatio = 0.5,
            criticalRatio = 0.9,
            recoveryRatio = 0.2,
            criticalHoldSeconds = 3,
            recoveryHoldSeconds = 5,
            cooldownSeconds = 15,
            windowSeconds = 5,
            maxObservationGapSeconds = 3,
            minObservations = 3,
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var body = await saved.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        Assert.Equal("DATABASE", doc.RootElement.GetProperty("origin").GetString());
        Assert.True(doc.RootElement.GetProperty("version").GetInt32() > 0);
        Assert.NotEqual(before.RootElement.TryGetProperty("version", out var v) ? v.GetInt32() : 0,
            doc.RootElement.GetProperty("version").GetInt32());

        // GET sau PUT đọc lại đúng giá trị (nguồn DATABASE), và provider áp nóng lên RiskOptions dùng chung.
        using var scope = app.Services.CreateScope();
        var thresholds = scope.ServiceProvider.GetRequiredService<SmartPool.Application.Features.SafetyMonitoring.IRiskThresholds>();
        Assert.Equal(0.75, thresholds.Current.ConfidenceThreshold);
        var after = await client.GetFromJsonAsync<JsonDocument>(path);
        Assert.Equal("DATABASE", after.RootElement.GetProperty("origin").GetString());
        Assert.Equal(0.75, after.RootElement.GetProperty("confidenceThreshold").GetDouble());

        // Giá trị không hợp lệ (vi phạm RiskOptions.IsValid) → 400.
        var invalid = await client.PutAsJsonAsync(path, new
        {
            confidenceThreshold = 1.5,
            warningRatio = 0.5,
            criticalRatio = 0.9,
            recoveryRatio = 0.2,
            criticalHoldSeconds = 3,
            recoveryHoldSeconds = 5,
            cooldownSeconds = 15,
            windowSeconds = 5,
            maxObservationGapSeconds = 3,
            minObservations = 3,
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await app.StopAsync();
    }

    [CloudinarySafetyFact]
    public async Task Snapshot_RealCloudinary_BackgroundRenderAndUpload_UpdateConfirmedIncident()
    {
        await using var app = await ApiAsync(realCloudinary: true);
        var id = Guid.Empty;
        try
        {
            await using var db = new SmartPoolDbContext(options);
            var command = Event("CRITICAL") with { ImageUrl = null };
            Assert.Empty(await db.Warnings.ToListAsync());
            var outbox = app.Services.GetRequiredService<SmartPool.Infrastructure.SafetyMonitoring.AlertSnapshotQueue>();
            Assert.True(await outbox.StageAsync(command,
                await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot(), "frontend/thanh-minh-smart-pool/src/assets/anh-be-boi.jpg")), default));
            var deadline = DateTime.UtcNow.AddSeconds(75);
            Warning? warning;
            do
            {
                await Task.Delay(1000);
                warning = await db.Warnings.AsNoTracking().SingleOrDefaultAsync();
            } while (DateTime.UtcNow < deadline && warning == null);
            Assert.NotNull(warning);
            id = warning.Id;
            Assert.Equal("COMPLETED", warning.SnapshotStatus);
            Assert.StartsWith("https://res.cloudinary.com/", warning.ImageUrl);
            await Repo(db).HandleAsync(id, "ACKNOWLEDGED", userId, true, default);
            Assert.Equal(warning.ImageUrl, (await db.Incidents.AsNoTracking().SingleAsync(x => x.WarningId == id)).ImageIncidents);
            Assert.Equal(1, await db.Incidents.CountAsync());
        }
        finally
        {
            await app.StopAsync();
            if (id != Guid.Empty)
            {
                var cleanup = await app.Services.GetRequiredService<CloudinaryDotNet.Cloudinary>()
                    .DestroyAsync(new CloudinaryDotNet.Actions.DeletionParams($"smart-pool/incidents/{id:N}"));
                Assert.True(cleanup.Error == null, "Cloudinary test asset cleanup failed.");
            }
        }
    }
}

public sealed class NativeSafetyFactAttribute : FactAttribute
{
    public NativeSafetyFactAttribute()
    {
        if (!File.Exists(Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_PYTHON")) ||
            !File.Exists(Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_FFPROBE")) ||
            !File.Exists(Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")))
            Skip = "Requires local ONNX/Python and FFmpeg/FFprobe; set SMARTPOOL_SAFETY_TEST_* and SMARTPOOL_CAMERA_TEST_FFMPEG.";
    }
}

public sealed class CloudinarySafetyFactAttribute : FactAttribute
{
    public CloudinarySafetyFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPOOL_CLOUDINARY_TEST_CLOUD_NAME")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPOOL_CLOUDINARY_TEST_API_KEY")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPOOL_CLOUDINARY_TEST_API_SECRET")) ||
            !File.Exists(Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_PYTHON")))
            Skip = "Requires an explicitly configured Cloudinary test account and Python snapshot renderer.";
    }
}

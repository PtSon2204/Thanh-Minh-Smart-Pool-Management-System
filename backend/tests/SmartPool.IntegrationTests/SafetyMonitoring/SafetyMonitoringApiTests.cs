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
using SmartPool.Application.Interfaces;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    private readonly Dictionary<string, Guid> apiUserIds = new();
    private async Task<WebApplication> ApiAsync(bool native = false, bool realCloudinary = false)
    {
        await using var db = new SmartPoolDbContext(options);
        var role = new Role { Id = Guid.NewGuid(), Name = "ADMIN", Permissions = "[]", IsDeleted = false };
        db.Roles.Add(role);
        var user = await db.Users.SingleAsync(x => x.Id == userId); user.RoleId = role.Id;
        await db.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        if (native) builder.Logging.AddConsole().AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>(
            (category, level) => category == typeof(SmartPool.Infrastructure.SafetyMonitoring.VideoAnalysisWorker).FullName && level >= LogLevel.Warning);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = db.Database.GetConnectionString(),
            ["ConnectionStrings:Redis"] = "localhost:56379,abortConnect=false",
            ["JwtSettings:SecretKey"] = "safety-integration-tests-signing-key-at-least-32-bytes",
            ["JwtSettings:Issuer"] = "safety-tests", ["JwtSettings:Audience"] = "safety-tests", ["JwtSettings:AccessTokenExpirationMinutes"] = "5",
            ["CloudinarySettings:CloudName"] = "test", ["CloudinarySettings:ApiKey"] = "test", ["CloudinarySettings:ApiSecret"] = "test",
            ["SafetyMonitoring:Enabled"] = native.ToString(),
            ["SafetyMonitoring:PythonPath"] = Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_PYTHON"),
            ["SafetyMonitoring:WorkerPath"] = Path.Combine(RepositoryRoot(), "backend", "ai", "worker.py"),
            ["SafetyMonitoring:ModelPath"] = Path.Combine(RepositoryRoot(), "model", "best.onnx"),
            ["SafetyMonitoring:FfprobePath"] = Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_FFPROBE"),
            ["SafetyMonitoring:FontPath"] = Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_FONT"),
            ["SafetyMonitoring:StoragePath"] = Path.Combine(RepositoryRoot(), "backend", ".test-runtime", "jobs", database),
            ["CameraSettings:FfmpegPath"] = Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")
        });
        builder.Services.AddApplicationServices(builder.Configuration);
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
        foreach (var path in new[] { "/api/cameras", "/api/alerts", "/api/video-analyses/status" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "SALES"));
        foreach (var path in new[] { "/api/cameras", "/api/alerts", "/api/video-analyses/status" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "LIFEGUARD"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/cameras")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/video-analyses", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "ADMIN"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/cameras")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/cameras?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/alerts?status=invalid")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "Admin"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/video-analyses/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/video-analyses/{Guid.NewGuid()}")).StatusCode);
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

    [NativeSafetyFact]
    public async Task VideoUpload_RealOnnxBackgroundJob_RendersPlayableH264AndSupportsRangeRequests()
    {
        await using var app = await ApiAsync(native: true);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "Admin"));
        var root = Path.Combine(RepositoryRoot(), "backend", ".test-runtime", "jobs", database);
        Directory.CreateDirectory(root); var input = Path.Combine(root, "fixture.mp4");
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")!) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-y", "-loop", "1", "-framerate", "10", "-i", Path.Combine(RepositoryRoot(), "frontend/thanh-minh-smart-pool/src/assets/anh-be-boi.jpg"),
            "-f", "lavfi", "-i", "sine=frequency=440", "-t", "2", "-vf", "scale=640:360", "-c:v", "libx264", "-bf", "0", "-c:a", "alac", input + ".mp4" }) start.ArgumentList.Add(arg);
        var suppliedVideo = Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_VIDEO");
        if (!string.IsNullOrWhiteSpace(suppliedVideo)) File.Copy(suppliedVideo, input);
        else
        {
            using (var fixture = Process.Start(start)!) { var error = fixture.StandardError.ReadToEndAsync(); await fixture.WaitForExitAsync(); Assert.True(fixture.ExitCode == 0, await error); }
            // Fragmented MP4 with slightly longer audio: OpenCV reports 22 frames, but video has 20.
            // This must finish normally using the decoded timeline, rather than failing at EOF.
            var remux = new ProcessStartInfo(Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")!)
            { CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true };
            foreach (var arg in new[] { "-v", "error", "-y", "-i", input + ".mp4", "-af", "apad=pad_dur=0.2",
                "-c:v", "copy", "-c:a", "alac", "-movflags", "frag_keyframe+empty_moov", input }) remux.ArgumentList.Add(arg);
            using var mux = Process.Start(remux)!;
            var muxError = mux.StandardError.ReadToEndAsync(); await mux.WaitForExitAsync(); Assert.True(mux.ExitCode == 0, await muxError);
        }
        using var body = new MultipartFormDataContent();
        var content = new ByteArrayContent(await File.ReadAllBytesAsync(input)); content.Headers.ContentType = new("video/mp4"); body.Add(content, "file", "pool.mp4");
        var startupRuns = new List<object>();
        async Task<object> MeasureStartup(Guid id, Stopwatch clock, double acceptedMs)
        {
            double? firstFramesMs = null;
            VideoFrameAnalysisWindowDto sample;
            var measurementDeadline = DateTime.UtcNow.AddMinutes(3);
            do
            {
                sample = (await client.GetFromJsonAsync<VideoFrameAnalysisWindowDto>($"/api/video-analyses/{id}/frame-analysis/window?from=0&duration=1"))!;
                if (sample.ReadyFrames >= 2) firstFramesMs ??= clock.Elapsed.TotalMilliseconds;
                if (sample.Complete) break;
                var currentJob = (await client.GetFromJsonAsync<VideoJobDto>($"/api/video-analyses/{id}"))!;
                Assert.NotEqual("FAILED", currentJob.Status);
                await Task.Delay(25);
            } while (DateTime.UtcNow < measurementDeadline);
            Assert.True(sample.Complete);
            Assert.NotNull(firstFramesMs);
            var analysisMs = clock.Elapsed.TotalMilliseconds - acceptedMs;
            return new { uploadAndValidationMs = acceptedMs, firstSynchronizedFramesAfterAcceptedMs = firstFramesMs - acceptedMs,
                analysisCompleteAfterAcceptedMs = analysisMs, frames = sample.ReadyFrames, fps = sample.Fps,
                speedIncludingProbeAndDb = sample.ReadyFrames / sample.Fps / (analysisMs / 1000) };
        }
        var startupClock = Stopwatch.StartNew();
        var response = await client.PostAsync("/api/video-analyses", body);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        var job = (await response.Content.ReadFromJsonAsync<VideoJobDto>())!;
        startupRuns.Add(await MeasureStartup(job.Id, startupClock, startupClock.Elapsed.TotalMilliseconds));
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (DateTime.UtcNow < deadline && job.Status is not ("COMPLETED" or "FAILED"))
        { await Task.Delay(500); job = (await client.GetFromJsonAsync<VideoJobDto>($"/api/video-analyses/{job.Id}"))!; }
        Assert.True(job.Status == "COMPLETED", job.Error ?? job.Status);
        var window = (await client.GetFromJsonAsync<VideoFrameAnalysisWindowDto>($"/api/video-analyses/{job.Id}/frame-analysis/window?from=0&duration=1"))!;
        Assert.True(window.Complete);
        Assert.False(window.Legacy);
        Assert.True(window.Fps > 0);
        Assert.NotEmpty(window.Frames);
        Assert.True(File.Exists(Path.Combine(root, $"{job.Id:N}-timeline.json")));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/video-analyses/{job.Id}/frame-analysis/window?duration=99")).StatusCode);
        // Legacy role names use title case. An admin must also be able to review another uploader's result.
        await using (var ownership = new SmartPoolDbContext(options))
            await ownership.VideoAnalysisJobs.Where(x => x.Id == job.Id).ExecuteUpdateAsync(set => set.SetProperty(x => x.UploadedBy, userId));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/alerts?sourceType=UPLOAD&jobId={job.Id}&page=1&pageSize=10")).StatusCode);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/video-analyses/{job.Id}/video"); request.Headers.Range = new RangeHeaderValue(0, 127);
        var video = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, video.StatusCode); Assert.Equal(128, (await video.Content.ReadAsByteArrayAsync()).Length);
        Assert.Equal("video/mp4", video.Content.Headers.ContentType!.MediaType);
        var entireVideo = await client.GetAsync($"/api/video-analyses/{job.Id}/video");
        Assert.Equal(HttpStatusCode.OK, entireVideo.StatusCode);
        Assert.True((await entireVideo.Content.ReadAsByteArrayAsync()).Length > 128);
        var playback = (await client.GetFromJsonAsync<VideoPlaybackDto>($"/api/video-analyses/{job.Id}/playback"))!;
        using var mediaClient = new HttpClient { BaseAddress = client.BaseAddress };
        var originalPlayback = (await client.GetFromJsonAsync<VideoPlaybackDto>($"/api/video-analyses/{job.Id}/playback?original=true"))!;
        Assert.Contains("original=true", originalPlayback.StreamPath);
        using (var originalRequest = new HttpRequestMessage(HttpMethod.Get, originalPlayback.StreamPath))
        {
            originalRequest.Headers.Range = new RangeHeaderValue(0, 127);
            var originalResponse = await mediaClient.SendAsync(originalRequest);
            Assert.Equal(HttpStatusCode.PartialContent, originalResponse.StatusCode);
            Assert.Equal((await File.ReadAllBytesAsync(input))[..128], await originalResponse.Content.ReadAsByteArrayAsync());
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await mediaClient.GetAsync($"/api/video-analyses/{job.Id}/playback")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await mediaClient.GetAsync($"/api/video-analyses/{job.Id}/stream")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await mediaClient.GetAsync(playback.StreamPath.Replace(job.Id.ToString(), Guid.NewGuid().ToString()))).StatusCode);
        using var mediaRequest = new HttpRequestMessage(HttpMethod.Get, playback.StreamPath);
        mediaRequest.Headers.Range = new RangeHeaderValue(128, 255);
        var media = await mediaClient.SendAsync(mediaRequest);
        Assert.Equal(HttpStatusCode.PartialContent, media.StatusCode);
        Assert.Equal(128, (await media.Content.ReadAsByteArrayAsync()).Length);
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, (await mediaClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, playback.StreamPath)
            { Headers = { Range = new RangeHeaderValue(long.MaxValue - 1, long.MaxValue) } })).StatusCode);
        var download = await mediaClient.GetAsync(playback.StreamPath + "&download=true");
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        var playbackUiScript = Environment.GetEnvironmentVariable("SMARTPOOL_VIDEO_PLAYBACK_UI_SCRIPT");
        if (!string.IsNullOrWhiteSpace(playbackUiScript))
        {
            var browser = new ProcessStartInfo("node") { CreateNoWindow = true, UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            browser.ArgumentList.Add(playbackUiScript);
            using var process = Process.Start(browser)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(new { apiBase = app.Urls.Single(),
                token = client.DefaultRequestHeaders.Authorization!.Parameter, userId = apiUserIds["Admin"], jobId = job.Id, root = RepositoryRoot(), input }));
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
            Assert.True(process.ExitCode == 0, (await stdout) + (await stderr));
        }
        await using var db = new SmartPoolDbContext(options); Assert.Empty(await db.Incidents.ToListAsync());
        var generated = Path.Combine(root, $"{job.Id:N}-result.mp4");
        var probe = new ProcessStartInfo(Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_FFPROBE")!) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var arg in new[] { "-v", "error", "-show_entries", "stream=codec_name,codec_type:format=duration", "-of", "json", generated }) probe.ArgumentList.Add(arg);
        using (var check = Process.Start(probe)!)
        { var output = await check.StandardOutput.ReadToEndAsync(); await check.WaitForExitAsync(); Assert.Contains("h264", output); Assert.Contains("aac", output); }
        // A second native upload exercises the retained model and reset tracker/decoder.
        using var secondBody = new MultipartFormDataContent();
        secondBody.Add(new ByteArrayContent(await File.ReadAllBytesAsync(input)), "file", "second.mp4");
        startupClock.Restart();
        var secondResponse = await client.PostAsync("/api/video-analyses", secondBody);
        Assert.Equal(HttpStatusCode.Accepted, secondResponse.StatusCode);
        var secondJob = (await secondResponse.Content.ReadFromJsonAsync<VideoJobDto>())!;
        startupRuns.Add(await MeasureStartup(secondJob.Id, startupClock, startupClock.Elapsed.TotalMilliseconds));
        var report = JsonSerializer.Serialize(new { fixture = "native ONNX, 640x360, 10 FPS, 2 seconds; HTTP and timeline included", runs = startupRuns }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(RepositoryRoot(), "backend", ".test-runtime", "video-startup-results.json"), report);
        while (DateTime.UtcNow < deadline && secondJob.Status is not ("COMPLETED" or "FAILED"))
        { await Task.Delay(100); secondJob = (await client.GetFromJsonAsync<VideoJobDto>($"/api/video-analyses/{secondJob.Id}"))!; }
        Assert.Equal("COMPLETED", secondJob.Status);
        var vfrFile = Path.Combine(root, "variable-rate.mp4");
        var variableRate = new ProcessStartInfo(Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")!)
        { CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-y", "-i", input, "-vf", "setpts=if(lt(N\\,5)\\,N/(10*TB)\\,(N+2)/(10*TB))", "-fps_mode", "vfr", "-an", vfrFile }) variableRate.ArgumentList.Add(arg);
        using (var variable = Process.Start(variableRate)!)
        { var error = variable.StandardError.ReadToEndAsync(); await variable.WaitForExitAsync(); Assert.True(variable.ExitCode == 0, await error); }
        var videoProbe = app.Services.GetRequiredService<SmartPool.Infrastructure.SafetyMonitoring.VideoProbe>();
        var vfrInfo = await videoProbe.InspectAsync(vfrFile, false, default);
        await using (var timeline = videoProbe.OpenTimeline(vfrFile, vfrInfo))
            await Assert.ThrowsAsync<ArgumentException>(async () => { while (await timeline.ReadAsync(default)) { } });
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
            var command = Event("CRITICAL");
            id = (await Repo(db).RecordAsync(command, default)).Alert.Id;
            await Repo(db).HandleAsync(id, "ACKNOWLEDGED", userId, true, default);
            Assert.Equal("", (await db.Incidents.SingleAsync(x => x.WarningId == id)).ImageIncidents);
            var files = app.Services.GetRequiredService<SmartPool.Infrastructure.SafetyMonitoring.MonitoringFiles>();
            File.Copy(Path.Combine(RepositoryRoot(), "frontend/thanh-minh-smart-pool/src/assets/anh-be-boi.jpg"), files.PathFor($"{id:N}-source.jpg"));
            await File.WriteAllTextAsync(files.PathFor($"{id:N}-boxes.json"), System.Text.Json.JsonSerializer.Serialize(new[] { command.Decision }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
            await db.Warnings.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SnapshotStatus, "PENDING"));
            var deadline = DateTime.UtcNow.AddSeconds(75);
            Warning warning;
            do
            {
                await Task.Delay(1000);
                warning = await db.Warnings.AsNoTracking().SingleAsync(x => x.Id == id);
            } while (DateTime.UtcNow < deadline && warning.SnapshotStatus is not ("COMPLETED" or "FAILED"));
            Assert.Equal("COMPLETED", warning.SnapshotStatus);
            Assert.StartsWith("https://res.cloudinary.com/", warning.ImageUrl);
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

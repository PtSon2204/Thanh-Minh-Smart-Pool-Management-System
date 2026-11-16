using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.SafetyMonitoring;
using Microsoft.EntityFrameworkCore;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    [LiveBrowserPlaybackFact]
    public async Task SimulatorBrowserPlayback_ThreeRealCameras_ReportsFrameCadenceAndHandlesShortStalls()
    {
        var script = Environment.GetEnvironmentVariable("SMARTPOOL_LIVE_BROWSER_SCRIPT");
        Assert.True(File.Exists(script), "Set SMARTPOOL_LIVE_BROWSER_SCRIPT to the opt-in browser runner.");
        var urls = JsonSerializer.Deserialize<string[]>(Environment.GetEnvironmentVariable("SMARTPOOL_SIMULATOR_TEST_URLS") ?? "[]")!;
        Assert.Equal(3, urls.Length);
        Assert.All(urls, url => Assert.True(new Uri(url).IsLoopback && new Uri(url).Scheme == "rtsp"));
        var first = new Uri(urls[0]);
        Assert.All(urls, url => Assert.Equal(first.Port, new Uri(url).Port));
        await using var app = await ApiAsync(native: true, simulator: first);
        try
        {
            await using (var db = new SmartPoolDbContext(options))
                await db.Equipments.Where(x => x.Id == cameraId).ExecuteUpdateAsync(set => set.SetProperty(x => x.IsDeleted, true));
            using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            var token = await TokenAsync(app, "ADMIN");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var ids = new List<Guid>();
            foreach (var (url, index) in urls.Select((url, index) => (url, index)))
            {
                var response = await client.PostAsJsonAsync("/api/equipments", new { name = $"Performance Camera {index + 1}",
                    category = "Camera", location = "Isolated performance test", cameraConnection = new { protocol = "RTSP", streamUrl = url } });
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                ids.Add(created.RootElement.GetProperty("id").GetGuid());
            }
            var monitoring = app.Services.GetRequiredService<IOptions<MonitoringOptions>>().Value;
            Assert.True(monitoring.Configured, "Native monitoring options must be configured.");
            var state = app.Services.GetRequiredService<CameraAnalysisState>();
            var frames = app.Services.GetRequiredService<ICameraFrameSource>();
            var cameras = app.Services.GetRequiredService<CameraService>();
            var deadline = DateTime.UtcNow.AddSeconds(40);
            var observed = new HashSet<Guid>();
            while (DateTime.UtcNow < deadline && observed.Count < ids.Count)
            {
                foreach (var id in ids.Where(id => state.Get(id).State == "Connected")) observed.Add(id);
                await Task.Delay(250);
            }
            var diagnostics = ids.Select(id => new { id, stream = cameras.GetStatus(id).State, captured = frames.Capture(id) != null,
                analysis = state.Get(id).State, monitoring.MaxLiveCameras, worker = app.Services.GetServices<IHostedService>().OfType<LiveMonitoringWorker>().Single().ExecuteTask?.Status.ToString() }).ToArray();
            await File.WriteAllTextAsync(Path.Combine(RepositoryRoot(), "backend/.test-runtime/fluid-server-diagnostics.json"), JsonSerializer.Serialize(diagnostics));
            Assert.Equal(ids.Count, observed.Count);
            var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(script!);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { apiBase = app.Urls.Single(), token, userId = apiUserIds["ADMIN"], root = RepositoryRoot() }));
            process.StandardInput.Close();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3)); }
            catch { try { if (!process.HasExited) process.Kill(true); } catch { } throw; }
            Assert.True(process.ExitCode == 0, await output + await error);
        }
        finally { await app.StopAsync(); }
    }
}

public sealed class LiveBrowserPlaybackFactAttribute : FactAttribute
{
    public LiveBrowserPlaybackFactAttribute()
    {
        if (!File.Exists(Environment.GetEnvironmentVariable("SMARTPOOL_LIVE_BROWSER_SCRIPT")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPOOL_SIMULATOR_TEST_URLS")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPOOL_SIMULATOR_TEST_URL")) ||
            !File.Exists(Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_PYTHON")))
            Skip = "Opt-in: requires the browser runner, three loopback simulator URLs and native AI tools.";
    }
}

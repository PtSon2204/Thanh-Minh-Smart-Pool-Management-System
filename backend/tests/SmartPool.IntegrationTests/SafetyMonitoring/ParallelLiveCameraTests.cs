using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.SafetyMonitoring;
using SmartPool.IntegrationTests.ManageEquipments;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    [NativeSafetyFact]
    public async Task ParallelLiveCameras_KeepProducingFrames_WhenOneCameraIsRemoved()
    {
        var ffmpeg = Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")!;
        await using var first = await LocalRtspCamera.StartAsync(ffmpeg);
        await using var second = await LocalRtspCamera.StartAsync(ffmpeg, color: "red");
        await using var app = await ApiAsync(native: true);
        var settings = app.Services.GetRequiredService<IOptions<CameraOptions>>().Value;
        settings.AllowedEndpoints.Add(new() { Host = "127.0.0.1", Port = first.Port });
        settings.AllowedEndpoints.Add(new() { Host = "127.0.0.1", Port = second.Port });
        var cameras = app.Services.GetRequiredService<CameraService>();
        var state = app.Services.GetRequiredService<CameraAnalysisState>();
        var secondId = Guid.NewGuid();
        var frames = new ConcurrentDictionary<Guid, int>();
        void OnFrame(Guid id, DateTimeOffset time, byte[] jpeg) => frames.AddOrUpdate(id, 1, (_, count) => count + 1);
        state.FrameReady += OnFrame;
        try
        {
            await using (var db = new SmartPoolDbContext(options))
            {
                db.Equipments.Add(new Equipment { Id = secondId, Name = "Parallel camera", Category = "Camera", Status = "Active" });
                // These sort ahead of the real cameras, but have no RTSP source.
                for (var i = 1; i <= 4; i++) db.Equipments.Add(new Equipment {
                    Id = Guid.Parse($"00000000-0000-0000-0000-{i:000000000000}"), Name = $"Unconfigured camera {i}", Category = "Camera", Status = "Active" });
                await db.SaveChangesAsync();
            }
            foreach (var (id, url) in new[] { (cameraId, first.Url), (secondId, second.Url) })
            {
                await using var registration = await cameras.PrepareAsync(new() { StreamUrl = url }, default);
                registration.Publish(id); registration.Confirm();
            }
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline && (frames.GetValueOrDefault(cameraId) < 5 || frames.GetValueOrDefault(secondId) < 5))
                await Task.Delay(100);
            Assert.True(frames.GetValueOrDefault(cameraId) >= 5);
            Assert.True(frames.GetValueOrDefault(secondId) >= 5);
            await using (var db = new SmartPoolDbContext(options))
                await db.Equipments.Where(x => x.Id == cameraId).ExecuteUpdateAsync(set => set.SetProperty(x => x.IsDeleted, true));
            await Task.Delay(2500);
            Assert.Null(state.Get(cameraId).FrameId);
            var before = frames[secondId];
            await Task.Delay(1000);
            Assert.True(frames[secondId] > before);
            Assert.Equal("Connected", state.Get(secondId).State);
        }
        finally { state.FrameReady -= OnFrame; await app.StopAsync(); }
    }
}

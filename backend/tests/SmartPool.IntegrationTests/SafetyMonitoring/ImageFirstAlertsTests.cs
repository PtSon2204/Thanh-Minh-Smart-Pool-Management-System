using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartPool.Infrastructure.SafetyMonitoring;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    [Fact]
    public async Task SnapshotWorker_FailureKeepsDatabaseEmpty_AndResumesAfterUploadedImageIsAvailable()
    {
        await using var app = await ApiAsync(snapshotPython: Path.Combine(RepositoryRoot(), "backend", ".test-runtime", "missing-python.exe"));
        try
        {
            var queue = app.Services.GetRequiredService<AlertSnapshotQueue>();
            Assert.True(await queue.StageAsync(Event() with { ImageUrl = null }, [0xff, 0xd8, 0xff, 0xd9], default));
            var path = Assert.Single(queue.PendingFiles());
            PendingAlertSnapshot pending;
            var deadline = DateTime.UtcNow.AddSeconds(20);
            do
            {
                await Task.Delay(100);
                pending = await queue.ReadAsync(path, default);
            } while (pending.Attempts == 0 && DateTime.UtcNow < deadline);
            Assert.True(pending.Attempts > 0);
            await using var db = new SmartPoolDbContext(options);
            Assert.Empty(await db.Warnings.AsNoTracking().ToListAsync());
            // Simulate a durable upload result recovered after restart; no real Cloudinary request in this test.
            await queue.SaveAsync(pending with { Command = pending.Command with { ImageUrl = "https://example.test/recovered-upload.jpg" },
                NextAttemptAt = DateTimeOffset.MinValue }, default);
            deadline = DateTime.UtcNow.AddSeconds(20);
            SmartPool.Domain.Entities.Warning? saved;
            do
            {
                await Task.Delay(100);
                saved = await db.Warnings.AsNoTracking().SingleOrDefaultAsync();
            } while (saved == null && DateTime.UtcNow < deadline);
            Assert.NotNull(saved);
            Assert.Equal("https://example.test/recovered-upload.jpg", saved.ImageUrl);
            Assert.Equal("COMPLETED", saved.SnapshotStatus);
            Assert.Equal(1, await db.Warnings.CountAsync());
        }
        finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://example.test/snapshot.jpg")]
    public async Task MissingOrUnverifiedImage_IsRejectedBeforeAnyAlertInsert(string? image)
    {
        await using var db = new SmartPoolDbContext(options);
        var command = Event() with { ImageUrl = image };
        await Assert.ThrowsAsync<ArgumentException>(() => Repo(db).RecordAsync(command, default));
        Assert.Empty(await db.Warnings.ToListAsync());
        Assert.Empty(events.Names);
    }

    [Fact]
    public async Task SnapshotPreparation_DoesNotPersistUntilImageExists_AndEscalationKeepsCorrectImage()
    {
        await using var db = new SmartPoolDbContext(options);
        var repo = Repo(db);
        var command = Event() with { ImageUrl = null };
        var target = await repo.PrepareSnapshotAsync(command, default);
        Assert.NotNull(target);
        Assert.Empty(await db.Warnings.ToListAsync());
        Assert.Empty(events.Names);
        var warning = await repo.RecordAsync(command with { AlertId = target, ImageUrl = "https://example.test/warning.jpg" }, default);
        Assert.Equal(target, warning.Alert.Id);
        Assert.Equal("COMPLETED", warning.Alert.SnapshotStatus);
        Assert.Null(await repo.PrepareSnapshotAsync(command, default));
        var critical = Event("CRITICAL") with { ImageUrl = null };
        Assert.Equal(target, await repo.PrepareSnapshotAsync(critical, default));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.RecordAsync(critical, default));
        var previous = await db.Warnings.AsNoTracking().SingleAsync();
        Assert.Equal("WARNING", previous.SeverityLevel);
        Assert.Equal("https://example.test/warning.jpg", previous.ImageUrl);
        var escalated = await repo.RecordAsync(critical with { ImageUrl = "https://example.test/critical.jpg" }, default);
        Assert.Equal(target, escalated.Alert.Id);
        Assert.Equal("https://example.test/critical.jpg", escalated.Alert.ImageUrl);
        Assert.Equal("CRITICAL", escalated.Alert.Severity);
        Assert.Equal(1, await db.Warnings.CountAsync());
    }
}

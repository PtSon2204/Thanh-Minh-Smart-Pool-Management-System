using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using SmartPool.Application.Features.ManageEquipments.Queries.GetCameraList;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;
using SmartPool.Infrastructure.Persistence.Repositories.Equipments;

namespace SmartPool.IntegrationTests;

public sealed partial class SafetyMonitoringTests : IAsyncLifetime
{
    private readonly string database = "smartpool_test_" + Guid.NewGuid().ToString("N");
    private DbContextOptions<SmartPoolDbContext> options = null!;
    private string adminConnection = "";
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid cameraId = Guid.NewGuid();
    private readonly Events events = new();

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(IntegrationTestDatabase.GetConnectionString());
        builder.Database = "postgres"; adminConnection = builder.ConnectionString;
        await using var connection = new NpgsqlConnection(adminConnection); await connection.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection)) await cmd.ExecuteNonQueryAsync();
        builder.Database = database; IntegrationTestDatabase.ValidateTarget(builder.ConnectionString);
        options = new DbContextOptionsBuilder<SmartPoolDbContext>().UseNpgsql(builder.ConnectionString).Options;
        await using var db = new SmartPoolDbContext(options);
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS smart_pool; CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\"; CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        // InitialCreate.Up is an existing empty baseline. Bootstrap the model, then mark that baseline
        // so the migration test can exercise THIS migration's Down/Up without inventing legacy DDL.
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS public.\"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL)");
        // Index onboarding được tạo bằng raw SQL (20261031054801_Up) nên không nằm trong model EF;
        // tạo lại để giả định "đã áp dụng mọi migration" thành thật (Down của migration đó DROP index này).
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS users_normalized_email_onboarding_key ON smart_pool.users (lower(btrim(email))) WHERE email IS NOT NULL;");
        foreach (var migration in db.Database.GetMigrations())
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO public.\"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({migration}, '8.0.11')");
        db.Users.Add(new() { Id = userId, Username = "monitoring-test", Status = "Active" });
        db.Equipments.Add(new() { Id = cameraId, Name = "Camera A", Category = "Camera", Location = "Area A", Status = "Active" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(adminConnection)) return;
        await using var connection = new NpgsqlConnection(adminConnection); await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await cmd.ExecuteNonQueryAsync();
    }
    private MonitoringRepository Repo(SmartPoolDbContext db) => new(db, events, Options.Create(new RiskOptions()), NullLogger<MonitoringRepository>.Instance);
    private RecordRiskCommand Event(string severity = "WARNING", string source = "LIVE", Guid? id = null, string? episode = null) =>
        new(source, id ?? cameraId, new("1", episode ?? "episode", severity, .9, 5, [.1, .1, .3, .5]), .9, DateTime.UtcNow);

    [Fact]
    public async Task Migration_PreservesExistingWarningsAndIncidents()
    {
        await using var db = new SmartPoolDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261030120000_RemoveSwimClassRegistrationPayments");
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO smart_pool.warnings (id, cam_id, warning_time, image_url, severity_level, confidence, status, created_at, updated_at) VALUES ({id}, {cameraId}, NOW(), 'https://example.test/legacy.jpg', 'WARNING', 0.9, 'Pending', NOW(), NOW())");
        await migrator.MigrateAsync();
        var legacy = await db.Warnings.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(cameraId, legacy.CamId); Assert.Equal("https://example.test/legacy.jpg", legacy.ImageUrl);
        Assert.Null(legacy.RiskEpisode); Assert.Equal("Pending", legacy.Status);
        Assert.Equal("PENDING", (await Repo(db).GetAsync(id, userId, true, default)).Status);
        Assert.Contains((await Repo(db).ListAsync(new(), userId, true, default)).Items, x => x.Id == id);
    }

    [Fact]
    public async Task CameraList_FiltersAtDatabase_ExcludesDeletedOtherCategories_AndCredentials()
    {
        await using var db = new SmartPoolDbContext(options);
        db.Equipments.AddRange(new Equipment { Id = Guid.NewGuid(), Name = "Camera Pump", Category = "Pump", Location = "Area A", Status = "Active" },
            new Equipment { Id = Guid.NewGuid(), Name = "Deleted", Category = "Camera", IsDeleted = true },
            new Equipment { Id = Guid.NewGuid(), Name = "camera B", Category = " Camera ", Location = "Area B", Status = "Maintenance",
                Metadata = "{\"_camera\":{\"protectedConfiguration\":\"secret\"}}" });
        await db.SaveChangesAsync();
        var repo = new CameraListRepository(db);
        Assert.Equal(2, (await repo.ListAsync(new(), default)).TotalCount);
        var result = await repo.ListAsync(new(Search: "Area B", Status: "Maintenance", Area: "Area B"), default);
        Assert.Single(result.Items);
        Assert.DoesNotContain("secret", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Empty((await repo.ListAsync(new(Search: "%"), default)).Items);
        Assert.Single((await repo.ListAsync(new(PageSize: 1), default)).Items);
        Assert.Equal(2, (await repo.AreasAsync(default)).Length);
    }

    [Fact]
    public async Task Episode_IsDeduplicatedAndEscalated_AndAcknowledgementCreatesOnlyOneIncident()
    {
        Guid alertId;
        await using (var db = new SmartPoolDbContext(options))
        {
            alertId = (await Repo(db).RecordAsync(Event(), default)).Alert.Id;
            Assert.Equal(alertId, (await Repo(db).RecordAsync(Event(), default)).Alert.Id);
            var escalated = await Repo(db).RecordAsync(Event("CRITICAL"), default);
            Assert.True(escalated.SnapshotRequired); Assert.Equal(alertId, escalated.Alert.Id);
            Assert.False((await Repo(db).RecordAsync(Event("CRITICAL"), default)).SnapshotRequired);
        }
        async Task<bool> Ack()
        {
            await using var db = new SmartPoolDbContext(options);
            try { await Repo(db).HandleAsync(alertId, "ACKNOWLEDGED", userId, true, default); return true; }
            catch (MonitoringConflictException) { return false; }
        }
        var results = await Task.WhenAll(Ack(), Ack());
        Assert.Single(results, x => x);
        await using var verify = new SmartPoolDbContext(options);
        Assert.Equal(1, await verify.Incidents.CountAsync(x => x.WarningId == alertId));
        Assert.Equal(new[] { "alert_created", "alert_escalated", "alert_updated", "incident_created" }, events.Names.ToArray());
    }

    [Theory]
    [InlineData("WARNING", "ACKNOWLEDGED")]
    [InlineData("CRITICAL", "CANCELLED")]
    public async Task WarningAcknowledgementAndCancellation_DoNotCreateIncident(string severity, string action)
    {
        await using var db = new SmartPoolDbContext(options);
        var alert = await Repo(db).RecordAsync(Event(severity), default);
        await Repo(db).HandleAsync(alert.Alert.Id, action, userId, true, default);
        Assert.Empty(await db.Incidents.ToListAsync());
        await Assert.ThrowsAsync<MonitoringConflictException>(() => Repo(db).HandleAsync(alert.Alert.Id, action, userId, true, default));
    }

    [Fact]
    public async Task UploadedVideo_RespectsOwner_AndNeverBroadcastsOrCreatesOperationalIncident()
    {
        var jobId = Guid.NewGuid();
        await using var db = new SmartPoolDbContext(options);
        db.VideoAnalysisJobs.Add(new() { Id = jobId, UploadedBy = userId, InputFile = "test.mp4", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var alert = await Repo(db).RecordAsync(Event("CRITICAL", "UPLOAD", jobId), default);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Repo(db).GetAsync(alert.Alert.Id, Guid.NewGuid(), false, default));
        await Repo(db).HandleAsync(alert.Alert.Id, "ACKNOWLEDGED", userId, false, default);
        Assert.Empty(await db.Incidents.ToListAsync()); Assert.Empty(events.Names); Assert.True(alert.SnapshotRequired);
    }

    [Fact]
    public async Task SnapshotAfterConfirmation_UpdatesTheAlreadyLinkedIncident()
    {
        Guid id;
        await using (var db = new SmartPoolDbContext(options))
        {
            id = (await Repo(db).RecordAsync(Event("CRITICAL"), default)).Alert.Id;
            await Repo(db).HandleAsync(id, "ACKNOWLEDGED", userId, true, default);
        }
        await using (var db = new SmartPoolDbContext(options)) await Repo(db).CompleteSnapshotAsync(id, "https://res.cloudinary.com/test/image/upload/incident.jpg", default);
        await using var verify = new SmartPoolDbContext(options);
        var warning = await verify.Warnings.SingleAsync(x => x.Id == id);
        var incident = await verify.Incidents.SingleAsync(x => x.WarningId == id);
        Assert.Equal(warning.ImageUrl, incident.ImageIncidents); Assert.Equal("COMPLETED", warning.SnapshotStatus);
    }

    [Fact]
    public async Task CancelledRisk_RearmsOnlyAfterCooldown_AndDoesNotCreateAlertEveryFrame()
    {
        await using var db = new SmartPoolDbContext(options);
        var repo = Repo(db); var first = await repo.RecordAsync(Event(), default);
        await repo.HandleAsync(first.Alert.Id, "CANCELLED", userId, true, default);
        Assert.Equal(first.Alert.Id, (await repo.RecordAsync(Event(), default)).Alert.Id);
        await db.Warnings.Where(x => x.Id == first.Alert.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.VerifiedAt, DateTime.UtcNow.AddSeconds(-16)));
        db.ChangeTracker.Clear();
        var second = await repo.RecordAsync(Event(), default);
        Assert.NotEqual(first.Alert.Id, second.Alert.Id);
        Assert.Equal(second.Alert.Id, (await repo.RecordAsync(Event(), default)).Alert.Id);
        Assert.Equal(2, await db.Warnings.CountAsync());
    }

    [Fact]
    public async Task AcknowledgedCritical_DoesNotCreateAnotherIncidentInTheSameRiskEpisode()
    {
        await using var db = new SmartPoolDbContext(options); var repo = Repo(db);
        var first = await repo.RecordAsync(Event("CRITICAL"), default);
        await repo.HandleAsync(first.Alert.Id, "ACKNOWLEDGED", userId, true, default);
        await db.Warnings.Where(x => x.Id == first.Alert.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.VerifiedAt, DateTime.UtcNow.AddSeconds(-16)));
        db.ChangeTracker.Clear();
        Assert.Equal(first.Alert.Id, (await repo.RecordAsync(Event("CRITICAL"), default)).Alert.Id);
        Assert.Equal(1, await db.Incidents.CountAsync()); Assert.Equal(1, await db.Warnings.CountAsync());
    }

    [Fact]
    public async Task AcknowledgedWarning_CanProduceOneCriticalFollowup_WithoutReopeningTheProcessedWarning()
    {
        await using var db = new SmartPoolDbContext(options); var repo = Repo(db);
        var warning = await repo.RecordAsync(Event(), default);
        await repo.HandleAsync(warning.Alert.Id, "ACKNOWLEDGED", userId, true, default);
        var critical = await repo.RecordAsync(Event("CRITICAL"), default);
        Assert.NotEqual(warning.Alert.Id, critical.Alert.Id); Assert.Equal("PENDING", critical.Alert.Status);
        Assert.Equal("ACKNOWLEDGED", (await repo.GetAsync(warning.Alert.Id, userId, true, default)).Status);
        Assert.Equal(critical.Alert.Id, (await repo.RecordAsync(Event("CRITICAL"), default)).Alert.Id);
        await repo.HandleAsync(critical.Alert.Id, "ACKNOWLEDGED", userId, true, default);
        Assert.Equal(1, await db.Incidents.CountAsync());
    }

    private sealed class Events : INotificationService
    {
        public ConcurrentQueue<string> Names { get; } = new();
        public Task SendToAllAsync(string name, object payload, CancellationToken ct = default) => throw new Exception("Monitoring may not broadcast to all users.");
        public Task SendToUserAsync(string user, string name, object payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToGroupAsync(string group, string name, object payload, CancellationToken ct = default)
        { Assert.Equal("safety-operators", group); Names.Enqueue(name); return Task.CompletedTask; }
    }
}

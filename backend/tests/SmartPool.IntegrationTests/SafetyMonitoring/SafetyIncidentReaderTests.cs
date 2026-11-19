using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.SafetyMonitoring;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    [Fact]
    public async Task IncidentReader_ExecutesLegacyProjectionFiltersCountsAndPaginationInPostgres()
    {
        await using var db = new SmartPoolDbContext(options);
        var time = DateTime.UtcNow;
        db.UserProfiles.Add(new() { UserId = userId, FullName = "Nguyễn An" });
        var manualId = Guid.NewGuid();
        db.Incidents.AddRange(
            new Incident { Id = manualId, Title = "Bể trẻ em", ReportedBy = userId, Status = "completed",
                ImageIncidents = "manual-photo.jpg", SeverityLevel = "WARNING", IncidentTime = time },
            new Incident { Id = Guid.NewGuid(), Title = "Manual", ReportedBy = userId, Location = "Khu vực A",
                Status = "processing", SeverityLevel = "EMERGENCY", IncidentTime = time.AddMinutes(-1) },
            new Incident { Id = Guid.NewGuid(), Title = "Camera A", CameraId = cameraId, Location = "Khu vực A",
                Status = "Resolved", SeverityLevel = "EMERGENCY", IncidentTime = time.AddMinutes(-2) },
            new Incident { Id = Guid.NewGuid(), Title = "Legacy Pending", CameraId = cameraId, Location = "Khu vực A",
                Status = "Pending", SeverityLevel = "EMERGENCY", IncidentTime = time.AddMinutes(-3) },
            new Incident { Id = Guid.NewGuid(), Title = "Legacy Vietnamese Pending", ReportedBy = userId, Location = "Bể lớn",
                Status = "chờ xử lý", SeverityLevel = "EMERGENCY", IncidentTime = time.AddMinutes(-4) });
        await db.SaveChangesAsync();
        var reader = new SafetyIncidentReader(db);
        var all = await reader.SearchAsync(new() { PageSize = 1 }, default);
        Assert.Single(all.Items);
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(1, all.CameraCount);
        Assert.Equal(2, all.LifeguardCount);
        Assert.Equal(0, all.PendingCount);
        Assert.Equal(2, all.ResolvedCount);
        Assert.Equal(1, all.InProgressCount);
        Assert.Equal(2, all.EmergencyCount);
        Assert.Equal(manualId, all.Items[0].Id);
        var manual = await reader.SearchAsync(new() { SourceType = "lifeguard", Search = "nguyễn an", Status = "resolved", Zone = "Bể trẻ em" }, default);
        var row = Assert.Single(manual.Items);
        Assert.Equal(manualId, row.Id);
        Assert.Equal("Nguyễn An", row.ReportedByName);
        Assert.Equal("Resolved", row.Status);
        Assert.Equal(0, manual.CameraCount);
        var byCode = await reader.SearchAsync(new() { Search = row.IncidentCode.ToLowerInvariant() }, default);
        Assert.Equal(manualId, Assert.Single(byCode.Items).Id);
        var camera = await reader.SearchAsync(new() { SourceType = "camera", PageSize = 1, SeverityLevel = "emergency",
            FromDate = time.AddMinutes(-3), ToDate = time.AddMinutes(-1) }, default);
        Assert.Single(camera.Items);
        Assert.Equal(1, camera.CameraCount);
        Assert.Equal(1, camera.TotalCount);
        var second = await reader.SearchAsync(new() { PageNumber = 2, PageSize = 1 }, default);
        Assert.Equal("In Progress", Assert.Single(second.Items).Status);
        var empty = await reader.SearchAsync(new() { Search = "no matching incident" }, default);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
    }
}

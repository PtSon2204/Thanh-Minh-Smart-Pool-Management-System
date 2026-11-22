using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    [Fact]
    public async Task CriticalAcknowledgement_RequiresSeparateIncidentResolution_AndPreservesHandledAlert()
    {
        Guid alertId, incidentId;
        await using (var db = new SmartPoolDbContext(options)) {
            var service = Repo(db);
            alertId = (await service.RecordAsync(Event("CRITICAL"), default)).Alert.Id;
            var handled = await service.HandleAsync(alertId, "ACKNOWLEDGED", userId, true, default);
            incidentId = handled.IncidentId!.Value;
            Assert.Equal("Pending", (await db.Incidents.SingleAsync(x => x.Id == incidentId)).Status);
        }
        await using var app = await ApiAsync();
        try {
            using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "LIFEGUARD"));
            foreach (var status in new[] { "In Progress", "Resolved" }) {
                using var response = await client.PutAsJsonAsync($"/api/safety-alerts/incidents/{incidentId}/confirm",
                    new { status, resolutionNote = "Đã kiểm tra tại hiện trường" });
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                await using var verify = new SmartPoolDbContext(options);
                var incident = await verify.Incidents.AsNoTracking().SingleAsync(x => x.Id == incidentId);
                Assert.Equal(status, incident.Status);
                Assert.Contains("Đã kiểm tra tại hiện trường", incident.Description);
                var alert = await verify.Warnings.AsNoTracking().SingleAsync(x => x.Id == alertId);
                Assert.Equal("ACKNOWLEDGED", alert.Status);
                Assert.Equal("CRITICAL", alert.SeverityLevel);
                Assert.Equal(incidentId, alert.IncidentId);
                Assert.Equal(1, await verify.Incidents.CountAsync(x => x.WarningId == alertId));
            }
        } finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData("xyzabc", 0)]
    [InlineData(" CAM ERA a ", 1)]
    [InlineData(" ar ea A ", 1)]
    [InlineData("ca.*", 0)]
    public async Task CameraSearch_ApiFiltersNameOrLocationAndReportsMatchingCount(string search, int expected)
    {
        await using var app = await ApiAsync();
        try {
            using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "ADMIN"));
            using var response = await client.GetAsync("/api/cameras?search=" + Uri.EscapeDataString(search));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expected, json.RootElement.GetProperty("totalCount").GetInt32());
            var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(expected, items.Length);
            if (expected == 1) Assert.Equal(cameraId, Assert.Single(items).GetProperty("id").GetGuid());
        } finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData("t")]
    [InlineData(" t ! ")]
    [InlineData("%_")]
    public async Task CameraSearch_ApiRejectsTooShortOrSpecialCharacterOnlyInput(string search)
    {
        await using var app = await ApiAsync();
        try {
            using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "LIFEGUARD"));
            using var response = await client.GetAsync("/api/cameras?search=" + Uri.EscapeDataString(search));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Contains("2 chữ cái hoặc chữ số", json.RootElement.GetProperty("message").GetString());
        } finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task AlertFilters_ApiReturnsDatabaseOptionsAndHandlerName()
    {
        await using (var db = new SmartPoolDbContext(options)) {
            db.UserProfiles.Add(new UserProfile { UserId = userId, FullName = "Nguyễn Văn An" });
            await db.SaveChangesAsync();
            var service = Repo(db);
            var alert = await service.RecordAsync(Event(), default);
            await service.HandleAsync(alert.Alert.Id, "ACKNOWLEDGED", userId, true, default);
        }
        await using var app = await ApiAsync();
        try {
            using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "ADMIN"));
            using var filters = JsonDocument.Parse(await client.GetStringAsync("/api/alerts/filters"));
            Assert.Equal("Area A", Assert.Single(filters.RootElement.GetProperty("areas").EnumerateArray()).GetString());
            Assert.Equal("WARNING", Assert.Single(filters.RootElement.GetProperty("severities").EnumerateArray()).GetString());
            Assert.Equal("ACKNOWLEDGED", Assert.Single(filters.RootElement.GetProperty("statuses").EnumerateArray()).GetString());
            Assert.Equal(cameraId, Assert.Single(filters.RootElement.GetProperty("cameras").EnumerateArray()).GetProperty("id").GetGuid());
            using var list = JsonDocument.Parse(await client.GetStringAsync($"/api/alerts?cameraId={cameraId}&area=Area%20A&severity=WARNING&status=ACKNOWLEDGED"));
            var row = Assert.Single(list.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("Nguyễn Văn An", row.GetProperty("handledByName").GetString());
            Assert.Equal(userId, row.GetProperty("handledBy").GetGuid());
        } finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task CameraSearch_MatchesOnlyNameOrLocationIgnoringCaseAndWhitespace()
    {
        await using var db = new SmartPoolDbContext(options);
        db.Equipments.Add(new Equipment { Id = Guid.NewGuid(), Name = "Other", Category = "Camera", Location = "Pool B", Status = "Active" });
        await db.SaveChangesAsync();
        var repository = new SmartPool.Infrastructure.Persistence.Repositories.Equipments.CameraListRepository(db);
        foreach (var search in new[] { " CAM ERA a ", " ar ea A " }) {
            var result = await repository.ListAsync(new(Search: search), default);
            Assert.Equal(1, result.TotalCount);
            Assert.Equal(cameraId, Assert.Single(result.Items).Id);
        }
        Assert.Empty((await repository.ListAsync(new(Search: "no matching camera"), default)).Items);
        Assert.Empty((await repository.ListAsync(new(Search: "a.*"), default)).Items);
    }

    [Fact]
    public async Task AlertFilters_UseDatabaseOptionsAndCombineBeforePagination()
    {
        await using var db = new SmartPoolDbContext(options);
        var otherCamera = Guid.NewGuid();
        db.Equipments.Add(new Equipment { Id = otherCamera, Name = "Camera B", Category = "Camera", Location = "Area B", Status = "Active" });
        await db.SaveChangesAsync();
        var observed = new DateTime(2026, 11, 21, 18, 0, 0, DateTimeKind.Utc);
        var first = await Repo(db).RecordAsync(Event("CRITICAL") with { ObservedAt = observed }, default);
        await Repo(db).RecordAsync(Event("CRITICAL", otherCamera), default);
        await Repo(db).RecordAsync(Event("WARNING", episode: "other-episode") with { ObservedAt = observed }, default);
        var repository = new MonitoringRepository(db);
        var filters = await repository.FiltersAsync(default);
        Assert.Contains("Area A", filters.Areas);
        Assert.Contains("Area B", filters.Areas);
        Assert.Contains("CRITICAL", filters.Severities);
        Assert.Contains("WARNING", filters.Severities);
        Assert.Contains("PENDING", filters.Statuses);
        Assert.Contains(filters.Cameras, x => x.Id == cameraId && x.Name == "Camera A");
        Assert.Contains(filters.Cameras, x => x.Id == otherCamera && x.Name == "Camera B");
        Assert.Contains("2026-11-22", filters.Dates); // 18:00 UTC is the next date in Vietnam.
        var query = new GetAlertsQuery(CameraId: cameraId, Area: "Area A", Severity: "CRITICAL", Status: "PENDING",
            FromTime: new DateTimeOffset(observed), ToTime: new DateTimeOffset(observed), PageSize: 1);
        var page = await repository.ListAsync(query, userId, true, default);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(first.Alert.Id, Assert.Single(page.Items).Id);
        Assert.Empty((await repository.ListAsync(query with { Area = "Area B" }, userId, true, default)).Items);
        Assert.Empty((await repository.ListAsync(query with { FromTime = new DateTimeOffset(observed.AddMilliseconds(1)), ToTime = null }, userId, true, default)).Items);
    }

    [Fact]
    public async Task AlertDetails_ShowHandlerProfileNameWithUsernameFallback()
    {
        await using var db = new SmartPoolDbContext(options);
        db.UserProfiles.Add(new UserProfile { UserId = userId, FullName = "Nguyễn Văn An" });
        await db.SaveChangesAsync();
        var service = Repo(db);
        var alert = await service.RecordAsync(Event(), default);
        Assert.Null(alert.Alert.HandledByName);
        var handled = await service.HandleAsync(alert.Alert.Id, "ACKNOWLEDGED", userId, true, default);
        Assert.Equal(userId, handled.HandledBy);
        Assert.Equal("Nguyễn Văn An", handled.HandledByName);
        await db.UserProfiles.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        Assert.Equal("monitoring-test", (await service.GetAsync(alert.Alert.Id, userId, true, default)).HandledByName);
    }
}

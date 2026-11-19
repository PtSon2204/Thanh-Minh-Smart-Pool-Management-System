using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    [Fact]
    public async Task CancelFromStaleWarningPopup_RequiresReviewOfEscalatedCritical()
    {
        await using var app = await ApiAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "ADMIN"));
            Guid id;
            await using (var db = new SmartPoolDbContext(options)) {
                var repo = Repo(db);
                id = (await repo.RecordAsync(Event("WARNING"), default)).Alert.Id;
                await repo.RecordAsync(Event("CRITICAL"), default);
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsync($"/api/alerts/{id}/cancel", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/alerts/{id}/cancel", new { expectedSeverity = "WARNING" })).StatusCode);
            await using (var verify = new SmartPoolDbContext(options)) {
                var pending = await verify.Warnings.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Equal("PENDING", pending.Status);
                Assert.Equal("CRITICAL", pending.SeverityLevel);
                Assert.Empty(await verify.Incidents.ToListAsync());
            }
            Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/alerts/{id}/cancel", new { expectedSeverity = "CRITICAL" })).StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("LIFEGUARD")]
    public async Task AcknowledgeCriticalLive_PersistsInIncidentHistory_AndCannotCreateDuplicate(string role)
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, role));
        Guid alertId;
        await using (var db = new SmartPoolDbContext(options))
            alertId = (await Repo(db).RecordAsync(Event("CRITICAL"), default)).Alert.Id;
        var response = await client.PatchAsJsonAsync($"/api/alerts/{alertId}/acknowledge", new { expectedSeverity = "CRITICAL" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var handled = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ACKNOWLEDGED", handled.RootElement.GetProperty("status").GetString());
        var incidentId = handled.RootElement.GetProperty("incidentId").GetGuid();
        using var history = JsonDocument.Parse(await client.GetStringAsync("/api/safety-alerts/incidents?sourceType=camera"));
        var incident = Assert.Single(history.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == incidentId);
        Assert.Equal("Area A", incident.GetProperty("zone").GetString());
        Assert.Equal("EMERGENCY", incident.GetProperty("severityLevel").GetString());
        Assert.Equal("Resolved", incident.GetProperty("status").GetString());
        Assert.Equal("Camera AI giám sát", incident.GetProperty("sourceType").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/alerts/{alertId}/acknowledge", new { expectedSeverity = "CRITICAL" })).StatusCode);
        await using var verify = new SmartPoolDbContext(options);
        Assert.Equal(1, await verify.Incidents.CountAsync(row => row.WarningId == alertId && row.CameraId == cameraId));
        Assert.Equal(incidentId, (await verify.Warnings.AsNoTracking().SingleAsync(row => row.Id == alertId)).IncidentId);
        await app.StopAsync();
    }

    [Theory]
    [InlineData("WARNING", "cancel")]
    [InlineData("CRITICAL", "cancel")]
    public async Task NonIncidentAlertActions_DoNotAddRowsToIncidentHistory(string severity, string action)
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "ADMIN"));
        Guid alertId;
        await using (var db = new SmartPoolDbContext(options))
            alertId = (await Repo(db).RecordAsync(Event(severity), default)).Alert.Id;
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/alerts/{alertId}/{action}", new { expectedSeverity = severity })).StatusCode);
        using var history = JsonDocument.Parse(await client.GetStringAsync("/api/safety-alerts/incidents"));
        Assert.Empty(history.RootElement.GetProperty("items").EnumerateArray());
        await using var verify = new SmartPoolDbContext(options);
        Assert.False(await verify.Incidents.AnyAsync(row => row.WarningId == alertId));
        await app.StopAsync();
    }

    [Fact]
    public async Task AcknowledgeWarningLive_PersistsInIncidentHistory_AsWarningResolved()
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "LIFEGUARD"));
        Guid alertId;
        await using (var db = new SmartPoolDbContext(options))
            alertId = (await Repo(db).RecordAsync(Event("WARNING"), default)).Alert.Id;
        var response = await client.PatchAsJsonAsync($"/api/alerts/{alertId}/acknowledge", new { expectedSeverity = "WARNING" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var handled = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var incidentId = handled.RootElement.GetProperty("incidentId").GetGuid();
        using var history = JsonDocument.Parse(await client.GetStringAsync("/api/safety-alerts/incidents?sourceType=camera&severityLevel=WARNING"));
        var incident = Assert.Single(history.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == incidentId);
        Assert.Equal("WARNING", incident.GetProperty("severityLevel").GetString());
        Assert.Equal("Resolved", incident.GetProperty("status").GetString());
        Assert.Equal("https://example.test/verified-snapshot.jpg", incident.GetProperty("imageIncidents").GetString());
        await app.StopAsync();
    }

    [Fact]
    public async Task WarningPopupEscalatedBeforeClick_RequiresReviewInsteadOfCreatingAnIncident()
    {
        await using var app = await ApiAsync();
        try {
            using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "ADMIN"));
            Guid id;
            await using (var db = new SmartPoolDbContext(options)) {
                var repo = Repo(db);
                id = (await repo.RecordAsync(Event("WARNING"), default)).Alert.Id;
                Assert.Equal(id, (await repo.RecordAsync(Event("CRITICAL"), default)).Alert.Id);
            }
            var missing = await client.PatchAsync($"/api/alerts/{id}/acknowledge", null);
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            var stale = await client.PatchAsJsonAsync($"/api/alerts/{id}/acknowledge", new { expectedSeverity = "WARNING" });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            using var conflict = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
            Assert.Equal("CRITICAL", conflict.RootElement.GetProperty("alert").GetProperty("severity").GetString());
            await using (var db = new SmartPoolDbContext(options)) {
                var warning = await db.Warnings.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Equal("PENDING", warning.Status);
                Assert.Null(warning.IncidentId);
                Assert.Empty(await db.Incidents.ToListAsync());
            }
            var accepted = await client.PatchAsJsonAsync($"/api/alerts/{id}/acknowledge", new { expectedSeverity = "CRITICAL" });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            await using var verify = new SmartPoolDbContext(options);
            Assert.Equal(1, await verify.Incidents.CountAsync(x => x.WarningId == id));
        } finally { await app.StopAsync(); }
    }

    [Fact]
    [Trait("TestCaseId", "MSA-AL-01")]
    public async Task SafetyOperatorCanViewAlertDetailsAndUnknownAlertReturnsNotFound()
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "ADMIN"));

        Guid alertId;
        await using (var db = new SmartPoolDbContext(options))
            alertId = (await Repo(db).RecordAsync(Event("WARNING"), default)).Alert.Id;

        var response = await client.GetAsync($"/api/alerts/{alertId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.Equal(alertId, body.RootElement.GetProperty("id").GetGuid());
            Assert.Equal("WARNING", body.RootElement.GetProperty("severity").GetString());
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/alerts/{Guid.NewGuid()}")).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/alerts/{alertId}")).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    [Trait("TestCaseId", "MSA-IN-01")]
    public async Task SafetyOperatorCanReadIncidentDetailsByIdAndUnknownIncidentReturnsNotFound()
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "LIFEGUARD"));

        var incidentId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow.AddMinutes(-10);
        await using (var db = new SmartPoolDbContext(options))
        {
            db.UserProfiles.Add(new UserProfile { UserId = userId, FullName = "Test Lifeguard" });
            db.Incidents.Add(new Incident
            {
                Id = incidentId,
                Title = "Rescue at main pool",
                Location = "Main Pool",
                Description = "A swimmer required assistance.",
                SeverityLevel = "EMERGENCY",
                Status = "In Progress",
                ReportedBy = userId,
                IncidentTime = occurredAt,
                ImageIncidents = "https://example.test/incident.jpg",
                CreatedAt = occurredAt,
                UpdatedAt = occurredAt,
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/safety-alerts/incidents/{incidentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var item = body.RootElement;
            Assert.Equal(incidentId, item.GetProperty("id").GetGuid());
            Assert.Equal("Main Pool", item.GetProperty("zone").GetString());
            Assert.Equal("In Progress", item.GetProperty("status").GetString());
            Assert.Equal("A swimmer required assistance.", item.GetProperty("description").GetString());
            Assert.Equal("Test Lifeguard", item.GetProperty("reportedByName").GetString());
            Assert.Equal("https://example.test/incident.jpg", item.GetProperty("imageIncidents").GetString());
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/safety-alerts/incidents/{Guid.NewGuid()}")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "SALES"));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/safety-alerts/incidents/{incidentId}")).StatusCode);
        await app.StopAsync();
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("LIFEGUARD")]
    [InlineData("Lifeguard")]
    [InlineData("lifeguard")]
    [Trait("TestCaseId", "MSA-HI-01")]
    public async Task SafetyOperatorCanViewFilteredIncidentHistory(string role)
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, role));

        var matchingId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow.AddMinutes(-5);
        await using (var db = new SmartPoolDbContext(options))
        {
            db.Incidents.AddRange(
                new Incident
                {
                    Id = pendingId, Title = "Unconfirmed pending incident", Location = "Kids Pool",
                    Description = "Legacy pending row must stay hidden.", SeverityLevel = "EMERGENCY", Status = "Pending",
                    ReportedBy = userId, IncidentTime = occurredAt.AddSeconds(10), CreatedAt = occurredAt, UpdatedAt = occurredAt,
                },
                new Incident
                {
                    Id = matchingId, Title = "Rescue at kids pool", Location = "Kids Pool",
                    Description = "A swimmer required help.", SeverityLevel = "EMERGENCY", Status = "Resolved",
                    ReportedBy = userId, IncidentTime = occurredAt, CreatedAt = occurredAt, UpdatedAt = occurredAt,
                },
                new Incident
                {
                    Id = Guid.NewGuid(), Title = "Routine report", Location = "Main Pool",
                    Description = "Unrelated incident.", SeverityLevel = "WARNING", Status = "Resolved",
                    ReportedBy = userId, IncidentTime = occurredAt.AddMinutes(-1), CreatedAt = occurredAt, UpdatedAt = occurredAt,
                });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/safety-alerts/incidents?zone=Kids%20Pool&pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var root = body.RootElement;
            Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
            Assert.Equal(0, root.GetProperty("pendingCount").GetInt32());
            Assert.Equal(1, root.GetProperty("resolvedCount").GetInt32());
            Assert.Equal(matchingId,
                root.GetProperty("items")[0].GetProperty("id").GetGuid());
        }
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/safety-alerts/incidents/{pendingId}")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "SALES"));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/safety-alerts/incidents")).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    [Trait("TestCaseId", "MSA-ACT-01")]
    public async Task LifeguardCanActivateRescueAlertAndInvalidOrUnauthorizedRequestsAreRejected()
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var request = new { zone = "Main Pool", level = "EMERGENCY", description = "Rescue required" };

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/safety-alerts/activate", request)).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "SALES"));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/safety-alerts/activate", request)).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "LIFEGUARD"));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/safety-alerts/activate", new { zone = " ", level = "EMERGENCY" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/safety-alerts/activate", new { zone = "Main Pool", level = "WARNING" })).StatusCode);

        var response = await client.PostAsJsonAsync("/api/safety-alerts/activate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var alertId = body.RootElement.GetProperty("alertId").GetGuid();
        Assert.Equal("Main Pool", body.RootElement.GetProperty("zone").GetString());

        await using var verify = new SmartPoolDbContext(options);
        var saved = await verify.Incidents.AsNoTracking().SingleAsync(x => x.Id == alertId);
        Assert.Equal("Cảnh báo cứu hộ: Main Pool", saved.Title);
        Assert.Equal("Resolved", saved.Status);
        Assert.Equal("Rescue required", saved.Description);
        Assert.Equal("Main Pool", saved.Location);
        await app.StopAsync();
    }
}

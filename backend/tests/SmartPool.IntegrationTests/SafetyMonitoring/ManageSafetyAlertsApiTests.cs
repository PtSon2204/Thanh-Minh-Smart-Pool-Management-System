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
            Assert.Equal("LIVE", body.RootElement.GetProperty("sourceType").GetString());
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

    [Fact]
    [Trait("TestCaseId", "MSA-HI-01")]
    public async Task SafetyOperatorCanViewFilteredIncidentHistory()
    {
        await using var app = await ApiAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(app, "LIFEGUARD"));

        var matchingId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow.AddMinutes(-5);
        await using (var db = new SmartPoolDbContext(options))
        {
            db.Incidents.AddRange(
                new Incident
                {
                    Id = matchingId, Title = "Rescue at kids pool", Location = "Kids Pool",
                    Description = "A swimmer required help.", SeverityLevel = "EMERGENCY", Status = "Pending",
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

        var response = await client.GetAsync("/api/safety-alerts/incidents?zone=Kids%20Pool&status=Pending&pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var root = body.RootElement;
            Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, root.GetProperty("pendingCount").GetInt32());
            Assert.Equal(matchingId,
                root.GetProperty("items")[0].GetProperty("id").GetGuid());
        }

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

        var response = await client.PostAsJsonAsync("/api/safety-alerts/activate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var alertId = body.RootElement.GetProperty("alertId").GetGuid();
        Assert.Equal("Main Pool", body.RootElement.GetProperty("zone").GetString());

        await using var verify = new SmartPoolDbContext(options);
        var saved = await verify.Incidents.AsNoTracking().SingleAsync(x => x.Id == alertId);
        Assert.Equal("Cảnh báo cứu hộ: Main Pool", saved.Title);
        Assert.Equal("Pending", saved.Status);
        Assert.Equal("Rescue required", saved.Description);
        Assert.Equal("Main Pool", saved.Location);
        await app.StopAsync();
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.ManageEquipments;

[Collection("Database")]
public sealed class EquipmentManagementApiTests
{
    [Fact]
    public async Task EquipmentAndInspectionUseCases_WorkThroughHttpAndPersistState()
    {
        await using var db = new SmartPoolDbContext(await IntegrationTestDatabase.CreateOptionsAsync());
        var suffix = Guid.NewGuid().ToString("N");
        var adminRole = await GetRoleAsync(db, "ADMIN");
        var lifeguardRole = await GetRoleAsync(db, "LIFEGUARD");
        var addedRoles = new[] { adminRole.Added ? adminRole.Role : null, lifeguardRole.Added ? lifeguardRole.Role : null }
            .Where(x => x is not null).Cast<Role>().ToList();
        var admin = TestUser(adminRole.Role, "eqadmin" + suffix);
        var lifeguard = TestUser(lifeguardRole.Role, "eqlifeguard" + suffix);
        var pump = new Equipment
        {
            Id = Guid.NewGuid(), Name = "Pump " + suffix, Category = "Pump", Location = "Pool A",
            PriorityLevel = "Critical", Metadata = "{\"power\":\"2kW\"}", Status = "Active", IsDeleted = false
        };
        var filter = new Equipment
        {
            Id = Guid.NewGuid(), Name = "Filter " + suffix, Category = "Filter", Location = "Pool A",
            Status = "Active", IsDeleted = false
        };
        db.Users.AddRange(admin, lifeguard);
        db.Equipments.AddRange(pump, filter);
        await db.SaveChangesAsync();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = IntegrationTestDatabase.GetConnectionString(),
            ["ConnectionStrings:Redis"] = IntegrationTestSettings.RedisConnection,
            ["JwtSettings:SecretKey"] = "equipment-management-tests-signing-key-at-least-32-bytes",
            ["JwtSettings:Issuer"] = "equipment-management-tests",
            ["JwtSettings:Audience"] = "equipment-management-tests",
            ["JwtSettings:AccessTokenExpirationMinutes"] = "5",
            ["CloudinarySettings:ApiKey"] = "test",
            ["CloudinarySettings:ApiSecret"] = "test",
            ["CloudinarySettings:CloudName"] = "test"
        });
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApiAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(EquipmentsController).Assembly);
        builder.Services.RemoveAll<TimeProvider>();
        var inspectionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(
            new DateTimeOffset(inspectionDate.ToDateTime(new TimeOnly(1, 0)), TimeSpan.Zero)));
        builder.Services.RemoveAll<IEmailService>();

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        var inspectionIds = new List<Guid>();
        var createdEquipmentIds = new List<Guid>();
        Guid? deletedEquipmentId = null;
        try
        {
            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using var scope = app.Services.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
            var adminToken = tokens.CreateToken(admin, "ADMIN").Token;
            var lifeguardToken = tokens.CreateToken(lifeguard, "LIFEGUARD").Token;

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/equipments")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/equipment-inspections/form")).StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lifeguardToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/equipments")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/equipments/{pump.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/equipment-inspections/form")).StatusCode);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var listResponse = await client.GetAsync("/api/equipments?searchTerm=" + suffix);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            using (var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()))
                Assert.Equal(2, listJson.RootElement.GetProperty("totalCount").GetInt32());

            var detailResponse = await client.GetAsync($"/api/equipments/{pump.Id}");
            Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
            using (var detailJson = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync()))
            {
                Assert.Equal(pump.Id, detailJson.RootElement.GetProperty("id").GetGuid());
                Assert.Equal("Pump " + suffix, detailJson.RootElement.GetProperty("name").GetString());
                Assert.Contains("2kW", detailJson.RootElement.GetProperty("metadata").GetString());
            }
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/equipments/{Guid.NewGuid()}")).StatusCode);
            var deleted = new Equipment { Id = Guid.NewGuid(), Name = "Deleted " + suffix, Category = "Pump", IsDeleted = true };
            deletedEquipmentId = deleted.Id;
            db.Equipments.Add(deleted);
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/equipments/{deleted.Id}")).StatusCode);

            var invalidCreate = await client.PostAsJsonAsync("/api/equipments", new { name = "", category = "Pump" });
            Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
            var createResponse = await client.PostAsJsonAsync("/api/equipments", new
            {
                name = "Created " + suffix, category = "Pump", location = "Pool B", metadata = new { power = "1kW" }
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<EquipmentRecord>();
            Assert.NotNull(created);
            createdEquipmentIds.Add(created.Id);
            var createdSaved = await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == created.Id);
            Assert.Equal("Pool B", createdSaved.Location);
            Assert.Equal("Active", createdSaved.Status);

            var updateResponse = await client.PutAsJsonAsync($"/api/equipments/{pump.Id}", new
            {
                name = "Updated " + suffix, category = "Pump", location = "Pool C", priorityLevel = "High",
                metadata = new { power = "3kW" }
            });
            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
            var updated = await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == pump.Id);
            Assert.Equal("Updated " + suffix, updated.Name);
            Assert.Equal("Pool C", updated.Location);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/equipments/{pump.Id}", new { name = "", category = "Pump" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/equipments/{Guid.NewGuid()}", new { name = "Missing", category = "Pump" })).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync($"/api/equipments/{pump.Id}/status", new { status = "Inactive" })).StatusCode);
            Assert.Equal("Inactive", (await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == pump.Id)).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/equipments/{pump.Id}/status", new { status = "Broken" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/equipments/{Guid.NewGuid()}/status", new { status = "Active" })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync($"/api/equipments/{pump.Id}/status", new { status = "Active" })).StatusCode);

            var form = await client.GetFromJsonAsync<InspectionFormResponse>("/api/equipment-inspections/form");
            Assert.NotNull(form);
            Assert.Contains(form.Equipments, x => x.EquipmentId == filter.Id);
            Assert.NotNull(form.CurrentShiftId);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lifeguardToken);
            var invalidInspection = await client.PostAsJsonAsync("/api/equipment-inspections", new
            {
                shiftId = form.CurrentShiftId, notes = "empty", items = Array.Empty<object>()
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidInspection.StatusCode);
            var submitResponse = await client.PostAsJsonAsync("/api/equipment-inspections", new
            {
                shiftId = form.CurrentShiftId,
                notes = "Start of shift " + suffix,
                items = new[]
                {
                    new { equipmentId = filter.Id, equipmentName = filter.Name, category = filter.Category,
                        location = filter.Location, isPassed = false, note = (string?)"Water leak", priorityLevel = "Normal" },
                    new { equipmentId = pump.Id, equipmentName = pump.Name, category = pump.Category,
                        location = pump.Location, isPassed = true, note = (string?)null, priorityLevel = "Critical" }
                }
            });
            Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);
            var inspection = await submitResponse.Content.ReadFromJsonAsync<InspectionRecord>();
            Assert.NotNull(inspection);
            inspectionIds.Add(inspection.Id);
            Assert.Equal("HasIssues", inspection.OverallStatus);
            Assert.Equal(1, inspection.IssuesCount);
            Assert.Equal("Maintenance", (await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == filter.Id)).Status);

            var fromDate = inspectionDate.ToString("yyyy-MM-dd");
            var historyResponse = await client.GetAsync($"/api/equipment-inspections?fromDate={fromDate}&toDate={fromDate}&overallStatus=HasIssues");
            Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
            using (var historyJson = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync()))
            {
                var row = Assert.Single(historyJson.RootElement.GetProperty("items").EnumerateArray(),
                    x => x.GetProperty("id").GetGuid() == inspection.Id);
                var issue = Assert.Single(row.GetProperty("items").EnumerateArray(),
                    x => x.GetProperty("equipmentId").GetGuid() == filter.Id);
                Assert.Equal("Pending", issue.GetProperty("resolutionStatus").GetString());
                Assert.Equal("Water leak", issue.GetProperty("note").GetString());
            }
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.PatchAsJsonAsync($"/api/equipment-inspections/{inspection.Id}/items/{filter.Id}/resolve",
                    new { action = "Repaired", resolutionImageUrls = new[] { "https://example.test/fixed.jpg" } })).StatusCode);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var resolveResponse = await client.PatchAsJsonAsync($"/api/equipment-inspections/{inspection.Id}/items/{filter.Id}/resolve", new
            {
                action = "Repaired", note = "Seal replaced", resolutionImageUrls = new[] { "https://example.test/fixed.jpg" }
            });
            Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
            var resolved = await resolveResponse.Content.ReadFromJsonAsync<InspectionRecord>();
            Assert.NotNull(resolved);
            Assert.Equal("Resolved", resolved.OverallStatus);
            Assert.Equal("Repaired", Assert.Single(resolved.Items, x => x.EquipmentId == filter.Id).ResolutionStatus);
            Assert.Equal("Active", (await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == filter.Id)).Status);

            var resolvedHistory = await client.GetAsync($"/api/equipment-inspections?fromDate={fromDate}&toDate={fromDate}&overallStatus=Resolved");
            Assert.Equal(HttpStatusCode.OK, resolvedHistory.StatusCode);
            using var resolvedJson = JsonDocument.Parse(await resolvedHistory.Content.ReadAsStringAsync());
            Assert.Contains(resolvedJson.RootElement.GetProperty("items").EnumerateArray(),
                x => x.GetProperty("id").GetGuid() == inspection.Id);
        }
        finally
        {
            await app.StopAsync();
            await db.DailyInspections.Where(x => inspectionIds.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Notifications.Where(x => x.UserId == admin.Id || x.UserId == lifeguard.Id).ExecuteDeleteAsync();
            var equipmentIds = new[] { pump.Id, filter.Id }.Concat(createdEquipmentIds)
                .Concat(deletedEquipmentId.HasValue ? [deletedEquipmentId.Value] : []).ToArray();
            await db.Equipments.Where(x => equipmentIds.Contains(x.Id) || x.Name.EndsWith(suffix)).ExecuteDeleteAsync();
            db.Users.RemoveRange(admin, lifeguard);
            db.Roles.RemoveRange(addedRoles);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<(Role Role, bool Added)> GetRoleAsync(SmartPoolDbContext db, string name)
    {
        var role = await db.Roles.FirstOrDefaultAsync(x => x.Name == name);
        if (role is not null) return (role, false);
        role = new Role { Id = Guid.NewGuid(), Name = name, IsDeleted = false };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return (role, true);
    }

    private static User TestUser(Role role, string username) => new()
    {
        Id = Guid.NewGuid(), Role = role, Username = username, PasswordHash = "test-only",
        Email = username + "@example.test", Phone = "09" + Guid.NewGuid().ToString("N")[..8], Status = "Active", IsDeleted = false
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class EquipmentRecord
    {
        public Guid Id { get; set; }
    }

    private sealed class InspectionFormResponse
    {
        public List<InspectionFormItem> Equipments { get; set; } = [];
        public Guid? CurrentShiftId { get; set; }
    }

    private sealed class InspectionFormItem
    {
        public Guid EquipmentId { get; set; }
    }

    private sealed class InspectionRecord
    {
        public Guid Id { get; set; }
        public string OverallStatus { get; set; } = string.Empty;
        public int IssuesCount { get; set; }
        public List<InspectionItemRecord> Items { get; set; } = [];
    }

    private sealed class InspectionItemRecord
    {
        public Guid EquipmentId { get; set; }
        public string? ResolutionStatus { get; set; }
    }
}

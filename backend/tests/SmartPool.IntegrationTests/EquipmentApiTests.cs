using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests;

public sealed class EquipmentApiTests
{
    [Fact]
    public async Task HttpEndpoints_EnforceJwtAndAdminPolicyAndValidateQueries()
    {
        await using var db = new SmartPoolDbContext(await IntegrationTestDatabase.CreateOptionsAsync());
        var suffix = Guid.NewGuid().ToString("N");
        var category = "create-" + suffix;
        db.Equipments.Add(new Equipment { Id = Guid.NewGuid(), Name = "Category source " + suffix, Category = category, Status = "Active", IsDeleted = false });
        var addedRoles = new List<Role>();
        var cameraGuards = new[] {
            new Equipment { Id = Guid.NewGuid(), Name = "Offline camera guard", Category = "Camera", Status = "Active", IsDeleted = false },
            new Equipment { Id = Guid.NewGuid(), Name = "Deleted camera guard", Category = "Camera", Status = "Active", IsDeleted = true }
        };
        async Task<Role> GetRole(string name)
        {
            var existing = await db.Roles.FirstOrDefaultAsync(x => x.Name == name);
            if (existing != null) return existing;
            var role = new Role { Id = Guid.NewGuid(), Name = name, IsDeleted = false };
            db.Roles.Add(role);
            addedRoles.Add(role);
            return role;
        }

        var admin = TestUser(await GetRole("ADMIN"), "admin" + suffix);
        var sales = TestUser(await GetRole("SALES"), "sales" + suffix);
        db.Users.AddRange(admin, sales);
        await db.SaveChangesAsync();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["JwtSettings:SecretKey"] = "equipment-integration-test-signing-key-at-least-32-bytes",
            ["JwtSettings:Issuer"] = "equipment-tests",
            ["JwtSettings:Audience"] = "equipment-tests",
            ["JwtSettings:AccessTokenExpirationMinutes"] = "5",
            ["CloudinarySettings:ApiKey"] = "test",
            ["CloudinarySettings:ApiSecret"] = "test",
            ["CloudinarySettings:CloudName"] = "test"
        });
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApiAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(EquipmentsController).Assembly);
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (Exception exception)
            {
                context.Response.StatusCode = 500;
                await Microsoft.AspNetCore.Http.HttpResponseWritingExtensions.WriteAsync(context.Response, exception.ToString());
            }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        try
        {
            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var viewPath = $"/api/equipments/{Guid.NewGuid()}/camera/views";
            var mediaPath = $"{viewPath}/{Guid.NewGuid()}";
            foreach (var path in new[] { "/api/equipments", "/api/equipments/filters", viewPath, mediaPath })
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(viewPath, null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync(viewPath + "/" + Guid.NewGuid())).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/equipments", new { name = "Pump", category })).StatusCode);

            using var scope = app.Services.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.CreateToken(sales, "SALES").Token);
            foreach (var path in new[] { "/api/equipments", "/api/equipments/filters" })
            {
                var response = await client.GetAsync(path);
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
            }
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/equipments", new { name = "Pump", category })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(viewPath, null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(mediaPath)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync(viewPath + "/" + Guid.NewGuid())).StatusCode);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.CreateToken(admin, "ADMIN").Token);
            var result = await client.GetFromJsonAsync<PagedResponse<EquipmentListItem>>("/api/equipments");
            Assert.NotNull(result);
            Assert.Equal(await db.Equipments.CountAsync(x => x.IsDeleted != true), result.TotalCount);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/equipments/filters")).StatusCode);
            var filterOptions = await client.GetFromJsonAsync<EquipmentFilterOptions>("/api/equipments/filters");
            Assert.NotNull(filterOptions);
            Assert.Contains("Active", filterOptions.Statuses);
            Assert.Contains("Maintenance", filterOptions.Statuses);
            Assert.Contains("Inactive", filterOptions.Statuses);
            foreach (var query in new[] { "pageIndex=0", "pageSize=101", "pageIndex=2147483647", "pageIndex=abc" })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/equipments?" + query)).StatusCode);

            foreach (var invalid in new[]
            {
                new { name = "", category, location = (string?)null },
                new { name = "   ", category, location = (string?)null },
                new { name = new string('x', 256), category, location = (string?)null },
                new { name = "Pump", category = "", location = (string?)null },
                new { name = "Pump", category = "missing-" + suffix, location = (string?)null },
                new { name = "Pump", category, location = (string?)new string('x', 256) }
            })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/equipments", invalid)).StatusCode);
            using (var malformed = new StringContent("{\"name\":\"Pump\",\"metadata\":{broken}}", Encoding.UTF8, "application/json"))
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/equipments", malformed)).StatusCode);

            var initialCount = await db.Equipments.CountAsync(x => x.Category == category);
            var minimalResponse = await client.PostAsJsonAsync("/api/equipments", new { name = "  Pump " + suffix + "  ", category });
            Assert.Equal(HttpStatusCode.Created, minimalResponse.StatusCode);
            Assert.NotNull(minimalResponse.Headers.Location);
            var minimal = await minimalResponse.Content.ReadFromJsonAsync<EquipmentListItem>();
            Assert.NotNull(minimal);
            var saved = await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == minimal.Id);
            Assert.Equal("Pump " + suffix, saved.Name);
            Assert.Equal("Active", saved.Status);
            Assert.False(saved.IsDeleted);
            Assert.Null(saved.Location);
            Assert.Null(saved.Metadata);
            Assert.NotNull(saved.CreatedAt);
            Assert.False(await db.MaintenanceSchedules.AnyAsync(x => x.EquipmentId == saved.Id));

            // Duplicate names are allowed. Overposted system fields cannot override server defaults.
            var fullResponse = await client.PostAsJsonAsync("/api/equipments", new
            {
                name = minimal.Name, category = " " + category + " ", location = "  Pool A  ",
                metadata = new { power = "2kW", dimensions = new { width = 20 }, sensors = new[] { 1, 2 } },
                status = "Inactive", isDeleted = true
            });
            Assert.Equal(HttpStatusCode.Created, fullResponse.StatusCode);
            var full = await fullResponse.Content.ReadFromJsonAsync<EquipmentListItem>();
            Assert.NotNull(full);
            var fullSaved = await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == full.Id);
            Assert.Equal("Pool A", fullSaved.Location);
            Assert.Equal("Active", fullSaved.Status);
            Assert.False(fullSaved.IsDeleted);
            using (var metadata = JsonDocument.Parse(fullSaved.Metadata!))
                Assert.Equal("2kW", metadata.RootElement.GetProperty("power").GetString());
            Assert.Equal(initialCount + 2, await db.Equipments.CountAsync(x => x.Category == category));
            var updated = await client.GetFromJsonAsync<PagedResponse<EquipmentListItem>>("/api/equipments?searchTerm=" + suffix + "&status=Active");
            Assert.NotNull(updated);
            Assert.Contains(updated.Items, x => x.Id == minimal.Id);
            Assert.Contains(updated.Items, x => x.Id == full.Id);

            db.Equipments.AddRange(cameraGuards);
            await db.SaveChangesAsync();
            var offer = new CameraOffer("offer", "v=0\r\n");
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(viewPath, offer)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/equipments/{minimal.Id}/camera/views", offer)).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync($"/api/equipments/{cameraGuards[0].Id}/camera/views", offer)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/equipments/{cameraGuards[1].Id}/camera/views", offer)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/equipments/{minimal.Id}/camera/views/{Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/equipments/{cameraGuards[1].Id}/camera/views/{Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/equipments/{cameraGuards[0].Id}/camera/views/{Guid.NewGuid()}/index.m3u8")).StatusCode);

            // The production token validator also rejects accounts whose current status changed.
            sales.Status = "Inactive";
            await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.CreateToken(sales, "SALES").Token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/equipments")).StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await db.Equipments.Where(x => x.Category == category).ExecuteDeleteAsync();
            var guardIds = cameraGuards.Select(x => x.Id).ToArray();
            await db.Equipments.Where(x => guardIds.Contains(x.Id)).ExecuteDeleteAsync();
            db.Users.RemoveRange(admin, sales);
            db.Roles.RemoveRange(addedRoles);
            await db.SaveChangesAsync();
        }
    }

    private static User TestUser(Role role, string username) => new()
    {
        Id = Guid.NewGuid(), Role = role, Username = username, PasswordHash = "test-only",
        Email = username + "@example.test", Phone = username[..1] + username[^15..], Status = "Active", IsDeleted = false
    };
}

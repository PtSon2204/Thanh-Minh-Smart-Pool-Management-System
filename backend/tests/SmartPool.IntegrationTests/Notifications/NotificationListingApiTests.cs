using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application;
using SmartPool.Application.Features.Notifications.Queries.GetNotifications;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.Notifications;

[Collection("NotificationsIntegration")]
public sealed class NotificationListingApiTests
{
    [Fact]
    public async Task NotificationListing_EnforcesAuthenticationAndRolePermissions()
    {
        await using var fixture = await Fixture.CreateAsync();

        // Chưa đăng nhập -> 401 cho cả danh sách Admin và danh sách cá nhân
        fixture.ClearAuth();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/v1/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/v1/notifications/mine")).StatusCode);

        // Không có quyền notifications:read -> 403 trên GET /api/v1/notifications, nhưng vẫn xem được /mine
        fixture.Use("LIFEGUARD");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/api/v1/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/v1/notifications/mine")).StatusCode);

        // Chỉ có notifications:create (thiếu read) -> 403 trên GET /api/v1/notifications
        fixture.Use("NOTIF_CREATOR_ONLY");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/api/v1/notifications")).StatusCode);

        // CUSTOMER vẫn xem được danh sách cá nhân /mine
        fixture.Use("CUSTOMER");
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/v1/notifications/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/api/v1/notifications")).StatusCode);

        // ADMIN có notifications:read -> 200 OK
        fixture.Use("ADMIN");
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/v1/notifications")).StatusCode);

        // Tài khoản đã bị khóa (Inactive) -> 401 Unauthorized
        await fixture.DeactivateUserAsync("ADMIN");
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/v1/notifications")).StatusCode);
    }

    [Fact]
    public async Task AdminNotificationListing_FiltersSortsPaginatesAndSyncsUnreadCountWithPostgreSql()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Use("ADMIN");

        var baseline = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>("/api/v1/notifications?page=1&pageSize=1");
        Assert.NotNull(baseline);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var typeAlert = $"IT_ALERT_{suffix}";
        var typeSystem = $"IT_SYS_{suffix}";
        var baseTime = new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);

        // Seed 5 thông báo với CreatedAt tăng dần và trạng thái đọc khác nhau (gồm cả null)
        var n1 = await fixture.AddNotificationAsync(fixture.UserId("LIFEGUARD"), "Alert old unread", "Content 1", typeAlert, false, baseTime.AddMinutes(1));
        var n2 = await fixture.AddNotificationAsync(fixture.UserId("CUSTOMER"), "System read", "Content 2", typeSystem, true, baseTime.AddMinutes(2));
        var n3 = await fixture.AddNotificationAsync(fixture.UserId("ADMIN"), "Alert null unread", "Content 3", typeAlert, null, baseTime.AddMinutes(3));
        var n4 = await fixture.AddNotificationAsync(fixture.UserId("LIFEGUARD"), "System unread", "Content 4", typeSystem, false, baseTime.AddMinutes(4));
        var n5 = await fixture.AddNotificationAsync(fixture.UserId("ADMIN"), "Alert read", "Content 5", typeAlert, true, baseTime.AddMinutes(5));

        // 1. Bộ lọc không khớp -> 200 OK, Items rỗng, UnreadCount vẫn đếm đúng số chưa đọc
        var emptyFiltered = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>(
            $"/api/v1/notifications?type=NON_EXISTENT_{suffix}");
        Assert.NotNull(emptyFiltered);
        Assert.Empty(emptyFiltered.Items);
        Assert.Equal(0, emptyFiltered.TotalCount);
        Assert.Equal(baseline.UnreadCount + 3, emptyFiltered.UnreadCount);

        // 2. Tổng số và UnreadCount toàn hệ thống tăng đúng theo dữ liệu vừa seed
        var afterSeed = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>("/api/v1/notifications?page=1&pageSize=10");
        Assert.NotNull(afterSeed);
        Assert.Equal(baseline.TotalCount + 5, afterSeed.TotalCount);
        Assert.Equal(baseline.UnreadCount + 3, afterSeed.UnreadCount);

        // 3. Lọc theo Type = typeAlert + sắp xếp CreatedAt giảm dần + phân trang trang 1 & 2
        var alertPage1 = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>(
            $"/api/v1/notifications?type={typeAlert}&page=1&pageSize=2");
        Assert.NotNull(alertPage1);
        Assert.Equal(3, alertPage1.TotalCount);
        Assert.Equal([n5, n3], alertPage1.Items.Select(x => x.Id).ToArray());

        var alertPage2 = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>(
            $"/api/v1/notifications?type={typeAlert}&page=2&pageSize=2");
        Assert.NotNull(alertPage2);
        Assert.Equal([n1], alertPage2.Items.Select(x => x.Id).ToArray());
        Assert.Equal("Alert old unread", alertPage2.Items[0].Title);
        Assert.False(alertPage2.Items[0].IsRead);

        // 4. Lọc kết hợp Type + IsRead=false (bao gồm cả dòng IsRead=null và false)
        var alertUnread = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>(
            $"/api/v1/notifications?type={typeAlert}&isRead=false");
        Assert.NotNull(alertUnread);
        Assert.Equal(2, alertUnread.TotalCount);
        Assert.Equal([n3, n1], alertUnread.Items.Select(x => x.Id).ToArray());
        Assert.All(alertUnread.Items, item => Assert.False(item.IsRead));

        // 5. Lọc theo Type + IsRead=true
        var alertRead = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>(
            $"/api/v1/notifications?type={typeAlert}&isRead=true");
        Assert.NotNull(alertRead);
        Assert.Single(alertRead.Items);
        Assert.Equal(n5, alertRead.Items[0].Id);
        Assert.True(alertRead.Items[0].IsRead);

        // 6. Kiểm tra clamp tham số phân trang biên (page <= 0, pageSize > 100)
        var clamped = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>(
            $"/api/v1/notifications?type={typeSystem}&page=0&pageSize=500");
        Assert.NotNull(clamped);
        Assert.Equal(1, clamped.Page);
        Assert.Equal(100, clamped.PageSize);
        Assert.Equal([n4, n2], clamped.Items.Select(x => x.Id).ToArray());

        // 7. Đánh dấu đã đọc 1 thông báo (n1) -> UnreadCount giảm 1 & lọc isRead=false cập nhật theo DB
        var markResponse = await fixture.Client.PatchAsync($"/api/v1/notifications/{n1}/read", null);
        Assert.Equal(HttpStatusCode.OK, markResponse.StatusCode);

        var alertUnreadAfterMark = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>(
            $"/api/v1/notifications?type={typeAlert}&isRead=false");
        Assert.NotNull(alertUnreadAfterMark);
        Assert.Equal(1, alertUnreadAfterMark.TotalCount);
        Assert.Equal([n3], alertUnreadAfterMark.Items.Select(x => x.Id).ToArray());
        Assert.Equal(baseline.UnreadCount + 2, alertUnreadAfterMark.UnreadCount);

        // 8. Tạo thông báo mới qua POST /api/v1/notifications -> GET danh sách thấy ngay và UnreadCount tăng 1
        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/notifications", new
        {
            userId = fixture.UserId("ADMIN"),
            title = "New realtime notice",
            content = "Water alert created",
            type = typeAlert
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdPayload = await createResponse.Content.ReadFromJsonAsync<CreatedIdResponse>();
        Assert.NotNull(createdPayload);
        fixture.TrackNotification(createdPayload.Id);

        var alertAfterCreate = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>(
            $"/api/v1/notifications?type={typeAlert}&page=1&pageSize=10");
        Assert.NotNull(alertAfterCreate);
        Assert.Equal(4, alertAfterCreate.TotalCount);
        Assert.Equal(baseline.UnreadCount + 3, alertAfterCreate.UnreadCount);
        Assert.Equal(createdPayload.Id, alertAfterCreate.Items[0].Id);
        Assert.False(alertAfterCreate.Items[0].IsRead);
    }

    [Fact]
    public async Task MyNotificationListing_EnforcesOwnerIsolationPaginationAndReadSyncOverHttp()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lifeguardId = fixture.UserId("LIFEGUARD");
        var customerId = fixture.UserId("CUSTOMER");
        var baseTime = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        var own1 = await fixture.AddNotificationAsync(lifeguardId, "LG Old", "C1", "STAFF_TASK", false, baseTime.AddMinutes(1));
        var own2 = await fixture.AddNotificationAsync(lifeguardId, "LG Mid Read", "C2", "STAFF_TASK", true, baseTime.AddMinutes(2));
        var own3 = await fixture.AddNotificationAsync(lifeguardId, "LG Newest", "C3", "WATER_QUALITY_TREND_DUTY", null, baseTime.AddMinutes(3));
        var other1 = await fixture.AddNotificationAsync(customerId, "Customer Private", "Secret", "SYSTEM", false, baseTime.AddMinutes(4));

        fixture.Use("LIFEGUARD");

        // Trang 1 (pageSize=2): chỉ thấy 2 thông báo mới nhất của chính LIFEGUARD
        var page1 = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>("/api/v1/notifications/mine?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.UnreadCount);
        Assert.Equal([own3, own2], page1.Items.Select(x => x.Id).ToArray());
        Assert.DoesNotContain(page1.Items, item => item.Id == other1);

        // Trang 2 (pageSize=2): trả về thông báo cũ còn lại
        var page2 = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>("/api/v1/notifications/mine?page=2&pageSize=2");
        Assert.NotNull(page2);
        Assert.Equal([own1], page2.Items.Select(x => x.Id).ToArray());

        // Giá trị phân trang biên được clamp an toàn [page >= 1, pageSize 1..100]
        var clamped = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>("/api/v1/notifications/mine?page=-2&pageSize=500");
        Assert.NotNull(clamped);
        Assert.Equal(1, clamped.Page);
        Assert.Equal(100, clamped.PageSize);
        Assert.Equal(3, clamped.Items.Count);

        // Không thể đánh dấu đã đọc thông báo của người khác -> 404
        var forbiddenMark = await fixture.Client.PatchAsync($"/api/v1/notifications/mine/{other1}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenMark.StatusCode);

        // Đánh dấu đã đọc thông báo của mình -> UnreadCount giảm còn 1, dữ liệu của CUSTOMER vẫn nguyên
        var ownMark = await fixture.Client.PatchAsync($"/api/v1/notifications/mine/{own3}/read", null);
        Assert.Equal(HttpStatusCode.OK, ownMark.StatusCode);

        var afterRead = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>("/api/v1/notifications/mine?page=1&pageSize=10");
        Assert.NotNull(afterRead);
        Assert.Equal(1, afterRead.UnreadCount);
        Assert.True(afterRead.Items.Single(x => x.Id == own3).IsRead);

        fixture.Use("CUSTOMER");
        var customerList = await fixture.Client.GetFromJsonAsync<GetNotificationsResult>("/api/v1/notifications/mine?page=1&pageSize=10");
        Assert.NotNull(customerList);
        Assert.Equal(1, customerList.TotalCount);
        Assert.Equal(1, customerList.UnreadCount);
        Assert.Equal(other1, Assert.Single(customerList.Items).Id);
    }

    private sealed class CreatedIdResponse
    {
        public Guid Id { get; set; }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DbContextOptions<SmartPoolDbContext> options;
        private readonly WebApplication app;
        private readonly Dictionary<string, User> users;
        private readonly Dictionary<string, string> tokens;
        private readonly HashSet<Guid> notificationIds = [];
        private readonly HashSet<Guid> userIds;
        private readonly HashSet<Guid> roleIds;

        private Fixture(DbContextOptions<SmartPoolDbContext> options, WebApplication app,
            Dictionary<string, User> users, HashSet<Guid> roleIds)
        {
            this.options = options;
            this.app = app;
            this.users = users;
            this.roleIds = roleIds;
            userIds = users.Values.Select(user => user.Id).ToHashSet();
            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using var scope = app.Services.CreateScope();
            var tokenService = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
            tokens = users.ToDictionary(pair => pair.Key,
                pair => tokenService.CreateToken(pair.Value, pair.Value.Role!.Name).Token);
        }

        public HttpClient Client { get; }

        public Guid UserId(string key) => users[key].Id;

        public void TrackNotification(Guid id) => notificationIds.Add(id);

        public void ClearAuth() => Client.DefaultRequestHeaders.Authorization = null;

        public void Use(string key) => Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens[key]);

        public static async Task<Fixture> CreateAsync()
        {
            var options = await IntegrationTestDatabase.CreateOptionsAsync();
            await using var db = new SmartPoolDbContext(options);
            var suffix = Guid.NewGuid().ToString("N");
            var definitions = new Dictionary<string, string>
            {
                ["ADMIN"] = """{"notifications":["read","create","update"]}""",
                ["NOTIF_CREATOR_ONLY"] = """{"notifications":["create"]}""",
                ["LIFEGUARD"] = """{"water_quality":["read"]}""",
                ["CUSTOMER"] = """{"profiles":["read","update"]}"""
            };
            var roles = definitions.ToDictionary(pair => pair.Key, pair => new Role
            {
                Id = Guid.NewGuid(),
                Name = $"IT_NOTIF_{pair.Key}_{suffix}",
                Permissions = pair.Value,
                IsDeleted = false
            });
            var phoneIdx = new Dictionary<string, int>
            {
                ["ADMIN"] = 1,
                ["NOTIF_CREATOR_ONLY"] = 2,
                ["LIFEGUARD"] = 3,
                ["CUSTOMER"] = 4
            };
            var users = roles.ToDictionary(pair => pair.Key, pair => new User
            {
                Id = Guid.NewGuid(),
                Role = pair.Value,
                Username = $"it_notif_{pair.Key.ToLowerInvariant()}_{suffix}",
                Email = $"it_notif_{pair.Key.ToLowerInvariant()}_{suffix}@example.test",
                Phone = $"09{suffix[..7]}{phoneIdx[pair.Key]}",
                PasswordHash = "test-only",
                Status = "ACTIVE",
                IsDeleted = false
            });

            db.Roles.AddRange(roles.Values);
            db.Users.AddRange(users.Values);
            db.UserProfiles.AddRange(users.Select(pair => new UserProfile
            {
                UserId = pair.Value.Id,
                FullName = $"IT {pair.Key}"
            }));
            await db.SaveChangesAsync();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = IntegrationTestDatabase.GetConnectionString(),
                ["ConnectionStrings:Redis"] = IntegrationTestSettings.RedisConnection,
                ["JwtSettings:SecretKey"] = "notification-integration-tests-signing-key-at-least-32-bytes",
                ["JwtSettings:Issuer"] = "notification-tests",
                ["JwtSettings:Audience"] = "notification-tests",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "5",
                ["CloudinarySettings:ApiKey"] = "test",
                ["CloudinarySettings:ApiSecret"] = "test",
                ["CloudinarySettings:CloudName"] = "test"
            });
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddInfrastructureServices(builder.Configuration);
            builder.Services.AddApiAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(NotificationsController).Assembly);
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            try
            {
                await app.StartAsync();
                return new Fixture(options, app, users, roles.Values.Select(role => role.Id).ToHashSet());
            }
            catch
            {
                await app.DisposeAsync();
                await using var cleanup = new SmartPoolDbContext(options);
                await cleanup.UserProfiles.Where(p => users.Values.Select(u => u.Id).Contains(p.UserId)).ExecuteDeleteAsync();
                await cleanup.Users.Where(u => users.Values.Select(x => x.Id).Contains(u.Id)).ExecuteDeleteAsync();
                await cleanup.Roles.Where(r => roles.Values.Select(x => x.Id).Contains(r.Id)).ExecuteDeleteAsync();
                throw;
            }
        }

        public async Task<Guid> AddNotificationAsync(
            Guid userId, string title, string content, string type, bool? isRead, DateTime createdAtUtc)
        {
            var id = Guid.NewGuid();
            notificationIds.Add(id);
            await using var db = new SmartPoolDbContext(options);
            db.Notifications.Add(new Notification
            {
                Id = id,
                UserId = userId,
                Title = title,
                Content = content,
                Type = type,
                IsRead = isRead,
                CreatedAt = createdAtUtc
            });
            await db.SaveChangesAsync();
            return id;
        }

        public async Task DeactivateUserAsync(string key)
        {
            await using var db = new SmartPoolDbContext(options);
            var user = await db.Users.SingleAsync(u => u.Id == users[key].Id);
            user.Status = "Inactive";
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
            await using var db = new SmartPoolDbContext(options);
            await db.Notifications.Where(n => notificationIds.Contains(n.Id) || userIds.Contains(n.UserId)).ExecuteDeleteAsync();
            await db.UserProfiles.Where(p => userIds.Contains(p.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync();
            await db.Roles.Where(r => roleIds.Contains(r.Id)).ExecuteDeleteAsync();
        }
    }
}

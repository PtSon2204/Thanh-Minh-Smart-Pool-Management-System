using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application;
using SmartPool.Application.Features.Notifications.Commands.SendEmailNotification;
using SmartPool.Application.Features.Notifications.Models;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Services;

namespace SmartPool.IntegrationTests.Notifications;

[Collection("NotificationsIntegration")]
public sealed class CustomerNotificationsAndSupportTests
{
    [Fact]
    public async Task CreateNotificationAndSignalRHub_EnforcesPermissionsAndDeliversRealtimeEvent()
    {
        await using var fixture = await Fixture.CreateAsync();

        // 1. Không token hoặc không có quyền notifications:create -> 401 / 403
        fixture.ClearAuth();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/notifications", new
        {
            userId = fixture.UserId("CUSTOMER"),
            title = "Blocked",
            content = "Blocked",
            type = "system"
        })).StatusCode);

        fixture.Use("READ_ONLY");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync("/api/v1/notifications", new
        {
            userId = fixture.UserId("CUSTOMER"),
            title = "Forbidden",
            content = "Forbidden",
            type = "system"
        })).StatusCode);

        // 2. Kết nối SignalR /hubs/notifications không có JWT -> bị từ chối (không cho handshake)
        using var unauthSocket = new ClientWebSocket();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            unauthSocket.ConnectAsync(fixture.HubUri(), CancellationToken.None));

        // 3. Kết nối SignalR với JWT hợp lệ -> tạo thông báo qua POST /api/v1/notifications -> nhận ReceiveNotification real-time
        using var socket = await fixture.ConnectHubAsync("CUSTOMER");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var events = new ConcurrentQueue<JsonDocument>();
        var receiverTask = Fixture.ReadHubMessagesAsync(socket, events, cts.Token);

        fixture.Use("ADMIN");
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var createResp = await fixture.Client.PostAsJsonAsync("/api/v1/notifications", new
        {
            userId = fixture.UserId("CUSTOMER"),
            title = $"Đóng cửa đột xuất {suffix}",
            content = "Bể bơi tạm đóng cửa do bảo trì khẩn cấp.",
            type = "announcement"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<IdPayload>();
        Assert.NotNull(created);
        fixture.TrackNotification(created.Id);

        var received = await Fixture.WaitForHubTargetAsync(events, "ReceiveNotification",
            args => args.GetProperty("id").GetGuid() == created.Id, TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        using (received)
        {
            var arg = received.RootElement.GetProperty("arguments")[0];
            Assert.Equal($"Đóng cửa đột xuất {suffix}", arg.GetProperty("title").GetString());
            Assert.Equal("announcement", arg.GetProperty("type").GetString());
        }

        await cts.CancelAsync();
        await receiverTask;
    }

    [Fact]
    public async Task EmailEndpoints_EnforcePermissionsValidateRecipientAndSendMassClosureNotice()
    {
        await using var fixture = await Fixture.CreateAsync();

        // 1. Kiểm tra phân quyền 401 & 403 cho email/send, email/preview, email/history
        fixture.ClearAuth();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/notifications/email/send", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/notifications/email/preview", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/v1/notifications/email/history")).StatusCode);

        fixture.Use("CUSTOMER");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync("/api/v1/notifications/email/send", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync("/api/v1/notifications/email/preview", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/api/v1/notifications/email/history")).StatusCode);

        // 2. Preview HTML Email template
        fixture.Use("ADMIN");
        var previewResp = await fixture.Client.PostAsJsonAsync("/api/v1/notifications/email/preview", new
        {
            templateType = "announcement",
            recipientName = "Trần Minh Anh",
            subject = "[Thanh Minh Pool] Tạm đóng cửa do thời tiết xấu",
            content = "Bể bơi tạm ngưng nhận khách từ 14:00 hôm nay."
        });
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        Assert.Equal("text/html", previewResp.Content.Headers.ContentType?.MediaType);
        var html = await previewResp.Content.ReadAsStringAsync();
        Assert.Contains("Trần Minh Anh", html);
        Assert.Contains("Tạm đóng cửa do thời tiết xấu", html);
        Assert.Contains("Bể bơi tạm ngưng nhận khách từ 14:00 hôm nay.", html);

        // 3. Single recipient: rỗng hoặc không tồn tại trong hệ thống -> Success = false, không gửi Email
        var emptyEmailRes = await (await fixture.Client.PostAsJsonAsync("/api/v1/notifications/email/send", new
        {
            targetAudience = "single",
            toEmail = "",
            subject = "Test",
            content = "Content",
            templateType = "custom"
        })).Content.ReadFromJsonAsync<SendEmailResult>();
        Assert.NotNull(emptyEmailRes);
        Assert.False(emptyEmailRes.Success);
        Assert.Empty(fixture.CapturedEmails);

        var unknownEmailRes = await (await fixture.Client.PostAsJsonAsync("/api/v1/notifications/email/send", new
        {
            targetAudience = "single",
            toEmail = "not-in-system@example.test",
            subject = "Test",
            content = "Content",
            templateType = "custom"
        })).Content.ReadFromJsonAsync<SendEmailResult>();
        Assert.NotNull(unknownEmailRes);
        Assert.False(unknownEmailRes.Success);
        Assert.Contains("không tồn tại", unknownEmailRes.Message);
        Assert.Empty(fixture.CapturedEmails);

        // 4. Gửi hàng loạt cho nhóm "customers" (VD: thông báo đóng cửa đột xuất)
        var closureSubject = $"[Đóng cửa khẩn] Bảo trì {Guid.NewGuid().ToString("N")[..6]}";
        var massCustResp = await fixture.Client.PostAsJsonAsync("/api/v1/notifications/email/send", new
        {
            targetAudience = "customers",
            subject = closureSubject,
            content = "Hồ bơi đóng cửa đột xuất trong ngày mai.",
            templateType = "announcement"
        });
        Assert.Equal(HttpStatusCode.OK, massCustResp.StatusCode);
        var massCustResult = await massCustResp.Content.ReadFromJsonAsync<SendEmailResult>();
        Assert.NotNull(massCustResult);
        Assert.True(massCustResult.Success);
        Assert.Contains(fixture.CapturedEmails, m => m.To == fixture.Email("CUSTOMER"));
        Assert.Contains(fixture.CapturedEmails, m => m.To == fixture.Email("CUSTOMER_2"));
        Assert.DoesNotContain(fixture.CapturedEmails, m => m.To == fixture.Email("STAFF"));

        // 5. Gửi hàng loạt cho nhóm "staff" -> chỉ gửi nhân viên, không gửi khách hàng
        fixture.CapturedEmails.Clear();
        var staffSubject = $"[Nội bộ] Họp an toàn {Guid.NewGuid().ToString("N")[..6]}";
        var massStaffResp = await fixture.Client.PostAsJsonAsync("/api/v1/notifications/email/send", new
        {
            targetAudience = "staff",
            subject = staffSubject,
            content = "Thông báo họp toàn bộ nhân viên.",
            templateType = "announcement"
        });
        Assert.Equal(HttpStatusCode.OK, massStaffResp.StatusCode);
        var massStaffResult = await massStaffResp.Content.ReadFromJsonAsync<SendEmailResult>();
        Assert.NotNull(massStaffResult);
        Assert.Contains(fixture.CapturedEmails, m => m.To == fixture.Email("STAFF"));
        Assert.DoesNotContain(fixture.CapturedEmails, m => m.To == fixture.Email("CUSTOMER"));

        // 6. Lịch sử email đã gửi (GET /api/v1/notifications/email/history) ghi nhận đầy đủ kể cả khi DB tích lũy nhiều user từ các test khác
        var historyLimit = Math.Max(100, massCustResult.TotalRecipients + massStaffResult.TotalRecipients + 20);
        var history = await fixture.Client.GetFromJsonAsync<List<EmailHistoryItem>>($"/api/v1/notifications/email/history?limit={historyLimit}");
        Assert.NotNull(history);
        Assert.Contains(history, h => h.Subject == closureSubject && h.Recipient == fixture.Email("CUSTOMER") && h.Status == "Thành công");
        Assert.Contains(history, h => h.Subject == staffSubject && h.Recipient == fixture.Email("STAFF") && h.Status == "Thành công");
    }

    [Fact]
    public async Task TicketExpiryReminderWorker_RemindsOnlyExpiringMonthlyPassesAndPreventsDuplicates()
    {
        await using var fixture = await Fixture.CreateAsync();

        var monthlyTypeId = await fixture.AddTicketTypeAsync("Vé tháng IT", "VE_THANG", 500000m, 30);
        var dailyTypeId = await fixture.AddTicketTypeAsync("Vé lượt IT", "VE_LUOT", 50000m, null);

        var customerId = fixture.UserId("CUSTOMER");
        var customer2Id = fixture.UserId("CUSTOMER_2");
        var nowUtc = DateTime.UtcNow;

        var qrExpiring = $"M{Guid.NewGuid().ToString("N")[..7]}";
        var qrExpiringSoon = $"S{Guid.NewGuid().ToString("N")[..7]}";
        var qrFarFuture = $"F{Guid.NewGuid().ToString("N")[..7]}";
        var qrExpired = $"E{Guid.NewGuid().ToString("N")[..7]}";
        var qrDaily = $"D{Guid.NewGuid().ToString("N")[..7]}";

        // Đủ điều kiện nhắc nhở (VE_THANG, ACTIVE / EXPIRING_SOON, còn hạn <= 3 ngày)
        await fixture.AddTicketAsync(monthlyTypeId, customerId, qrExpiring, "ACTIVE", nowUtc.AddDays(2));
        await fixture.AddTicketAsync(monthlyTypeId, customer2Id, qrExpiringSoon, "EXPIRING_SOON", nowUtc.AddDays(1));

        // Không thuộc diện nhắc nhở (còn > 3 ngày / đã hết hạn / vé lượt)
        await fixture.AddTicketAsync(monthlyTypeId, customerId, qrFarFuture, "ACTIVE", nowUtc.AddDays(10));
        await fixture.AddTicketAsync(monthlyTypeId, customerId, qrExpired, "ACTIVE", nowUtc.AddHours(-1));
        await fixture.AddTicketAsync(dailyTypeId, customerId, qrDaily, "ACTIVE", nowUtc.AddDays(1));

        // Mở WebSocket SignalR cho customer để nhận nhắc nhở real-time
        using var socket = await fixture.ConnectHubAsync("CUSTOMER");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var events = new ConcurrentQueue<JsonDocument>();
        var receiverTask = Fixture.ReadHubMessagesAsync(socket, events, cts.Token);

        // Chạy quét lần 1
        await fixture.RunTicketExpirySweepAsync();

        // Chỉ gửi Email cho đúng 2 vé tháng sắp hết hạn của customer & customer2
        Assert.Equal(2, fixture.CapturedEmails.Count(m =>
            m.To == fixture.Email("CUSTOMER") || m.To == fixture.Email("CUSTOMER_2")));
        Assert.Contains(fixture.CapturedEmails, m => m.To == fixture.Email("CUSTOMER") && m.Body.Contains(qrExpiring));
        Assert.Contains(fixture.CapturedEmails, m => m.To == fixture.Email("CUSTOMER_2") && m.Body.Contains(qrExpiringSoon));
        Assert.DoesNotContain(fixture.CapturedEmails, m => m.Body.Contains(qrFarFuture));
        Assert.DoesNotContain(fixture.CapturedEmails, m => m.Body.Contains(qrExpired));
        Assert.DoesNotContain(fixture.CapturedEmails, m => m.Body.Contains(qrDaily));

        // Xác minh DB notifications đã ghi nhận TicketRenewalReminder đúng cho 2 vé hợp lệ
        await using (var db = fixture.CreateContext())
        {
            var customerReminders = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == customerId && n.Type == "TicketRenewalReminder")
                .ToListAsync();
            Assert.Single(customerReminders);
            Assert.Contains(qrExpiring, customerReminders[0].Content);

            var customer2Reminders = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == customer2Id && n.Type == "TicketRenewalReminder")
                .ToListAsync();
            Assert.Single(customer2Reminders);
            Assert.Contains(qrExpiringSoon, customer2Reminders[0].Content);
        }

        // Xác minh SignalR đẩy ReceiveNotification tới đúng customer
        var sigMsg = await Fixture.WaitForHubTargetAsync(events, "ReceiveNotification",
            arg => (arg.GetProperty("content").GetString() ?? "").Contains(qrExpiring), TimeSpan.FromSeconds(5));
        Assert.NotNull(sigMsg);
        sigMsg.Dispose();

        // Chạy quét lần 2 liên tiếp (trong vòng 4 ngày) -> KHÔNG được gửi trùng
        var sentBeforeSecondRun = fixture.CapturedEmails.Count;
        await fixture.RunTicketExpirySweepAsync();
        Assert.Equal(sentBeforeSecondRun, fixture.CapturedEmails.Count);

        await using (var db = fixture.CreateContext())
        {
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == customerId && n.Type == "TicketRenewalReminder"));
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == customer2Id && n.Type == "TicketRenewalReminder"));
        }

        await cts.CancelAsync();
        await receiverTask;
    }

    private sealed class IdPayload
    {
        public Guid Id { get; set; }
    }

    private sealed record SentEmailRecord(string To, string Subject, string Body);

    private sealed class CapturingEmailService : IEmailService
    {
        public ConcurrentBag<SentEmailRecord> Sent { get; } = [];

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Sent.Add(new SentEmailRecord(to, subject, htmlBody));
            return Task.CompletedTask;
        }

        public Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            foreach (var recipient in recipients)
            {
                Sent.Add(new SentEmailRecord(recipient, subject, htmlBody));
            }
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DbContextOptions<SmartPoolDbContext> options;
        private readonly WebApplication app;
        private readonly Dictionary<string, User> users;
        private readonly Dictionary<string, string> tokens;
        private readonly HashSet<Guid> notificationIds = [];
        private readonly HashSet<Guid> ticketIds = [];
        private readonly HashSet<Guid> ticketTypeIds = [];
        private readonly HashSet<Guid> userIds;
        private readonly HashSet<Guid> roleIdsToDelete;

        private Fixture(
            DbContextOptions<SmartPoolDbContext> options,
            WebApplication app,
            Dictionary<string, User> users,
            HashSet<Guid> roleIdsToDelete,
            CapturingEmailService emailSpy)
        {
            this.options = options;
            this.app = app;
            this.users = users;
            this.roleIdsToDelete = roleIdsToDelete;
            EmailSpy = emailSpy;
            userIds = users.Values.Select(u => u.Id).ToHashSet();
            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using var scope = app.Services.CreateScope();
            var tokenService = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
            tokens = users.ToDictionary(pair => pair.Key,
                pair => tokenService.CreateToken(pair.Value, pair.Value.Role!.Name).Token);
        }

        public HttpClient Client { get; }
        public CapturingEmailService EmailSpy { get; }
        public ConcurrentBag<SentEmailRecord> CapturedEmails => EmailSpy.Sent;

        public Guid UserId(string key) => users[key].Id;
        public string Email(string key) => users[key].Email!;
        public void TrackNotification(Guid id) => notificationIds.Add(id);
        public void ClearAuth() => Client.DefaultRequestHeaders.Authorization = null;
        public void Use(string key) => Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens[key]);
        public SmartPoolDbContext CreateContext() => new(options);

        public Uri HubUri() => new UriBuilder(Client.BaseAddress!) { Scheme = "ws", Path = "/hubs/notifications" }.Uri;

        public async Task<ClientWebSocket> ConnectHubAsync(string userKey)
        {
            var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", "Bearer " + tokens[userKey]);
            await socket.ConnectAsync(HubUri(), CancellationToken.None);
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"),
                WebSocketMessageType.Text, true, CancellationToken.None);
            return socket;
        }

        public static async Task ReadHubMessagesAsync(
            ClientWebSocket socket, ConcurrentQueue<JsonDocument> queue, CancellationToken token)
        {
            var buffer = new byte[16384];
            var pending = string.Empty;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(buffer.AsMemory(), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    pending += Encoding.UTF8.GetString(buffer, 0, result.Count);
                    int idx;
                    while ((idx = pending.IndexOf('\u001e')) >= 0)
                    {
                        var raw = pending[..idx];
                        pending = pending[(idx + 1)..];
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            queue.Enqueue(JsonDocument.Parse(raw));
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (WebSocketException) when (token.IsCancellationRequested) { }
        }

        public static async Task<JsonDocument?> WaitForHubTargetAsync(
            ConcurrentQueue<JsonDocument> queue, string targetName, Func<JsonElement, bool> predicate, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                while (queue.TryDequeue(out var doc))
                {
                    if (doc.RootElement.TryGetProperty("target", out var targetProp) &&
                        string.Equals(targetProp.GetString(), targetName, StringComparison.Ordinal) &&
                        doc.RootElement.TryGetProperty("arguments", out var argsProp) &&
                        argsProp.GetArrayLength() > 0 &&
                        predicate(argsProp[0]))
                    {
                        return doc;
                    }
                    doc.Dispose();
                }
                await Task.Delay(50);
            }
            return null;
        }

        public async Task RunTicketExpirySweepAsync()
        {
            var worker = new TicketExpiryReminderWorker(
                app.Services.GetRequiredService<IServiceScopeFactory>(),
                app.Services.GetRequiredService<ILogger<TicketExpiryReminderWorker>>());
            await worker.RunReminderSweepAsync(CancellationToken.None);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var options = await IntegrationTestDatabase.CreateOptionsAsync();
            await using var db = new SmartPoolDbContext(options);
            var suffix = Guid.NewGuid().ToString("N");

            var customerRole = await db.Roles.FirstAsync(r => r.Name == "CUSTOMER" && r.IsDeleted != true);
            var lifeguardRole = await db.Roles.FirstAsync(r => r.Name == "LIFEGUARD" && r.IsDeleted != true);

            var adminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = $"IT_CNS_ADMIN_{suffix}",
                Permissions = """{"notifications":["read","create","update"]}""",
                IsDeleted = false
            };
            var readOnlyRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = $"IT_CNS_READ_{suffix}",
                Permissions = """{"notifications":["read"]}""",
                IsDeleted = false
            };
            db.Roles.AddRange(adminRole, readOnlyRole);

            var users = new Dictionary<string, User>
            {
                ["ADMIN"] = new()
                {
                    Id = Guid.NewGuid(), RoleId = adminRole.Id, Role = adminRole,
                    Username = $"it_cns_admin_{suffix}", Email = $"it_cns_admin_{suffix}@example.test",
                    Phone = $"09{suffix[..7]}1", PasswordHash = "test-only", Status = "ACTIVE", IsDeleted = false
                },
                ["READ_ONLY"] = new()
                {
                    Id = Guid.NewGuid(), RoleId = readOnlyRole.Id, Role = readOnlyRole,
                    Username = $"it_cns_read_{suffix}", Email = $"it_cns_read_{suffix}@example.test",
                    Phone = $"09{suffix[..7]}2", PasswordHash = "test-only", Status = "ACTIVE", IsDeleted = false
                },
                ["STAFF"] = new()
                {
                    Id = Guid.NewGuid(), RoleId = lifeguardRole.Id, Role = lifeguardRole,
                    Username = $"it_cns_staff_{suffix}", Email = $"it_cns_staff_{suffix}@example.test",
                    Phone = $"09{suffix[..7]}3", PasswordHash = "test-only", Status = "ACTIVE", IsDeleted = false
                },
                ["CUSTOMER"] = new()
                {
                    Id = Guid.NewGuid(), RoleId = customerRole.Id, Role = customerRole,
                    Username = $"it_cns_cust1_{suffix}", Email = $"it_cns_cust1_{suffix}@example.test",
                    Phone = $"09{suffix[..7]}4", PasswordHash = "test-only", Status = "ACTIVE", IsDeleted = false
                },
                ["CUSTOMER_2"] = new()
                {
                    Id = Guid.NewGuid(), RoleId = customerRole.Id, Role = customerRole,
                    Username = $"it_cns_cust2_{suffix}", Email = $"it_cns_cust2_{suffix}@example.test",
                    Phone = $"09{suffix[..7]}5", PasswordHash = "test-only", Status = "ACTIVE", IsDeleted = false
                }
            };

            db.Users.AddRange(users.Values);
            db.UserProfiles.AddRange(users.Select(pair => new UserProfile
            {
                UserId = pair.Value.Id,
                FullName = $"CNS {pair.Key}"
            }));
            await db.SaveChangesAsync();

            var spy = new CapturingEmailService();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = IntegrationTestDatabase.GetConnectionString(),
                ["ConnectionStrings:Redis"] = IntegrationTestSettings.RedisConnection,
                ["JwtSettings:SecretKey"] = "customer-notifications-it-signing-key-at-least-32-bytes",
                ["JwtSettings:Issuer"] = "customer-notifications-tests",
                ["JwtSettings:Audience"] = "customer-notifications-tests",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "5",
                ["EmailSettings:Username"] = "smtp-integration-test@example.test",
                ["CloudinarySettings:ApiKey"] = "test",
                ["CloudinarySettings:ApiSecret"] = "test",
                ["CloudinarySettings:CloudName"] = "test"
            });
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddInfrastructureServices(builder.Configuration);
            builder.Services.AddSingleton(spy);
            builder.Services.AddScoped<IEmailService>(sp => sp.GetRequiredService<CapturingEmailService>());
            builder.Services.AddSingleton<IEmailHistoryStore, EmailHistoryStore>();
            builder.Services.AddApiAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(NotificationsController).Assembly);

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapHubs();
            app.MapControllers();

            var customRoleIds = new HashSet<Guid> { adminRole.Id, readOnlyRole.Id };
            try
            {
                await app.StartAsync();
                return new Fixture(options, app, users, customRoleIds, spy);
            }
            catch
            {
                await app.DisposeAsync();
                await using var cleanup = new SmartPoolDbContext(options);
                await cleanup.UserProfiles.Where(p => users.Values.Select(u => u.Id).Contains(p.UserId)).ExecuteDeleteAsync();
                await cleanup.Users.Where(u => users.Values.Select(x => x.Id).Contains(u.Id)).ExecuteDeleteAsync();
                await cleanup.Roles.Where(r => customRoleIds.Contains(r.Id)).ExecuteDeleteAsync();
                throw;
            }
        }

        public async Task<Guid> AddTicketTypeAsync(string name, string category, decimal price, int? durationDays)
        {
            var id = Guid.NewGuid();
            ticketTypeIds.Add(id);
            await using var db = CreateContext();
            db.TicketTypes.Add(new TicketType
            {
                Id = id,
                Name = name,
                TicketCategory = category,
                Price = price,
                DurationDays = durationDays,
                TotalEntries = 1,
                IsActive = true,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
            return id;
        }

        public async Task<Guid> AddTicketAsync(
            Guid typeId, Guid ownerId, string qrCode, string status, DateTime expiryUtc)
        {
            var id = Guid.NewGuid();
            ticketIds.Add(id);
            await using var db = CreateContext();
            db.Tickets.Add(new Ticket
            {
                Id = id,
                TicketTypeId = typeId,
                UserId = ownerId,
                QrCode = qrCode,
                Status = status,
                IssueDate = DateTime.UtcNow.AddDays(-28),
                ExpiryDate = expiryUtc,
                RemainingEntries = 1,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow.AddDays(-28)
            });
            await db.SaveChangesAsync();
            return id;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
            await using var db = CreateContext();
            await db.Notifications.Where(n => notificationIds.Contains(n.Id) || userIds.Contains(n.UserId)).ExecuteDeleteAsync();
            await db.Tickets.Where(t => ticketIds.Contains(t.Id)).ExecuteDeleteAsync();
            await db.TicketTypes.Where(t => ticketTypeIds.Contains(t.Id)).ExecuteDeleteAsync();
            await db.UserProfiles.Where(p => userIds.Contains(p.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync();
            await db.Roles.Where(r => roleIdsToDelete.Contains(r.Id)).ExecuteDeleteAsync();
        }
    }
}

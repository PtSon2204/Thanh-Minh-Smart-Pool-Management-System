using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.Notifications.Models;
using SmartPool.Application.Features.Notifications.Services;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Infrastructure.Persistence.DbContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartPool.Infrastructure.Services;

/// <summary>
/// Background worker tự động quét các gói vé tháng sắp hết hạn (trước 3 ngày)
/// và gửi email thông báo kèm ngày hết hạn chính xác đến khách hàng.
/// </summary>
public sealed class TicketExpiryReminderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TicketExpiryReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TicketExpiryReminderWorker khởi động...");

        // Chờ 15 giây sau khi ứng dụng khởi động rồi chạy lần quét đầu tiên
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var historyStore = scope.ServiceProvider.GetService<IEmailHistoryStore>();
                var notificationService = scope.ServiceProvider.GetService<INotificationService>();
                var timeProvider = scope.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

                await CheckAndSendRemindersAsync(db, emailService, historyStore, notificationService, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi xảy ra trong quá trình quét vé tháng sắp hết hạn.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CheckAndSendRemindersAsync(
        SmartPoolDbContext db,
        IEmailService emailService,
        IEmailHistoryStore? historyStore,
        INotificationService? notificationService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        // Quét các vé có hạn kết thúc trong vòng 3 ngày tới (ngưỡng 3 ngày + 2 giờ buffer)
        var maxThreshold = nowUtc.AddDays(3).AddHours(2);

        var candidateTickets = await db.Tickets
            .Include(t => t.User)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.TicketType)
            .Where(t =>
                !t.IsDeleted.GetValueOrDefault() &&
                (t.Status == "ACTIVE" || t.Status == "EXPIRING_SOON") &&
                t.UserId != null &&
                t.ExpiryDate != null &&
                t.ExpiryDate.Value > nowUtc &&
                t.ExpiryDate.Value <= maxThreshold &&
                (t.TicketType.TicketCategory == "VE_THANG" || t.TicketType.TicketCategory == nameof(TicketCategoryEnum.VE_THANG)))
            .ToListAsync(cancellationToken);

        if (candidateTickets.Count == 0) return;

        foreach (var ticket in candidateTickets)
        {
            if (ticket.User == null || string.IsNullOrWhiteSpace(ticket.User.Email) || !ticket.UserId.HasValue)
                continue;

            var userId = ticket.UserId.Value;

            // Kiểm tra xem đã gửi thông báo nhắc nhở cho vé này trong vòng 4 ngày gần nhất chưa
            bool alreadyNotified = await db.Notifications.AnyAsync(n =>
                n.UserId == userId &&
                n.Type == "TicketRenewalReminder" &&
                n.Content.Contains(ticket.QrCode) &&
                n.CreatedAt >= nowUtc.AddDays(-4), cancellationToken);

            if (alreadyNotified) continue;

            double hoursRemaining = (ticket.ExpiryDate!.Value - nowUtc).TotalHours;
            int daysRemaining = Math.Max(1, (int)Math.Ceiling(hoursRemaining / 24.0));
            string expiryDateFormatted = ticket.ExpiryDate.Value.ToString("dd/MM/yyyy HH:mm");
            string expiryDateShort = ticket.ExpiryDate.Value.ToString("dd/MM/yyyy");
            string recipientName = ticket.User.UserProfile?.FullName ?? ticket.User.Username ?? "Quý hội viên";

            string subject = $"[Thanh Minh Smart Pool] Nhắc nhở: Gói vé bơi tháng của bạn sẽ hết hạn vào ngày {expiryDateShort}";

            var meta = new Dictionary<string, string>
            {
                ["ticketCode"] = ticket.QrCode,
                ["expiryDate"] = expiryDateFormatted,
                ["daysRemaining"] = daysRemaining.ToString()
            };

            string htmlBody = EmailTemplateBuilder.BuildHtml("ticket_reminder", recipientName, subject, null, meta);

            try
            {
                await emailService.SendAsync(ticket.User.Email, subject, htmlBody, cancellationToken);

                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = $"Gói vé tháng sắp hết hạn (còn {daysRemaining} ngày)",
                    Content = $"Vé bơi tháng mã {ticket.QrCode} của bạn sẽ hết hạn vào ngày {expiryDateShort}. Vui lòng gia hạn sớm để không bị gián đoạn tập luyện!",
                    Type = "TicketRenewalReminder",
                    IsRead = false,
                    CreatedAt = nowUtc
                };

                await db.Notifications.AddAsync(notification, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                if (notificationService != null)
                {
                    try
                    {
                        await notificationService.SendToUserAsync(
                            userId.ToString(),
                            "ReceiveNotification",
                            new
                            {
                                id = notification.Id,
                                title = notification.Title,
                                content = notification.Content,
                                type = notification.Type,
                                createdAt = notification.CreatedAt
                            },
                            cancellationToken);
                    }
                    catch (Exception sigEx)
                    {
                        logger.LogWarning(sigEx, "Không thể gửi SignalR notification cho user {UserId}", userId);
                    }
                }

                if (historyStore != null)
                {
                    await historyStore.AddLogAsync(new EmailHistoryItem
                    {
                        Recipient = ticket.User.Email,
                        RecipientName = recipientName,
                        Subject = subject,
                        TemplateType = "ticket_reminder",
                        Status = "Thành công",
                        SentAt = nowUtc,
                        HtmlBody = htmlBody
                    });
                }

                logger.LogInformation("Đã tự động gửi email nhắc gia hạn vé {TicketCode} cho {Email} (hết hạn: {ExpiryDate})",
                    ticket.QrCode, ticket.User.Email, expiryDateShort);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Thất bại khi gửi email nhắc gia hạn vé {TicketCode} cho {Email}",
                    ticket.QrCode, ticket.User.Email);

                if (historyStore != null)
                {
                    await historyStore.AddLogAsync(new EmailHistoryItem
                    {
                        Recipient = ticket.User.Email,
                        RecipientName = recipientName,
                        Subject = subject,
                        TemplateType = "ticket_reminder",
                        Status = "Thất bại",
                        ErrorMessage = ex.Message,
                        SentAt = nowUtc,
                        HtmlBody = htmlBody
                    });
                }
            }
        }
    }
}

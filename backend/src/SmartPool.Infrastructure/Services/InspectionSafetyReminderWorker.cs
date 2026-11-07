using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Services;

/// <summary>
/// Worker chạy nền định kỳ kiểm tra các sự cố thiết bị chưa được Admin xác nhận xử lý.
/// Cứ 2 tiếng sẽ tự động gửi email nhắc nhở 1 lần cho đến khi hoàn thành sửa chữa/thay thế.
/// </summary>
public sealed class InspectionSafetyReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InspectionSafetyReminderWorker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public InspectionSafetyReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<InspectionSafetyReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InspectionSafetyReminderWorker đã khởi động.");

        // Kiểm tra mỗi 10 phút một lần
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessPendingRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong chu kỳ quét gửi email nhắc nhở thiết bị sự cố.");
            }
        }

        _logger.LogInformation("InspectionSafetyReminderWorker đã dừng.");
    }

    private async Task ProcessPendingRemindersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var timeProvider = scope.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var today = VietnamTimeBoundary.GetDate(timeProvider);
        var currentTime = VietnamTimeBoundary.GetTime(timeProvider);

        // Chỉ gửi email trong khung giờ hoạt động của bể bơi (05:30 - 20:00)
        if (currentTime < new TimeOnly(5, 30) || currentTime > new TimeOnly(20, 0))
        {
            return;
        }

        // Lấy các biên bản kiểm tra trong ngày hôm nay có sự cố chưa xử lý xong
        var unresolvedInspections = await dbContext.DailyInspections
            .AsNoTracking()
            .Where(x => x.InspectionDate == today && (x.OverallStatus == "HasIssues" || x.OverallStatus == "InResolution"))
            .ToListAsync(cancellationToken);

        if (unresolvedInspections.Count == 0) return;

        // Lấy danh sách email của Admin
        var adminEmails = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsDeleted != true && u.Role != null && u.Role.Name.ToUpper() == "ADMIN" && !string.IsNullOrWhiteSpace(u.Email))
            .Select(u => u.Email!.Trim())
            .Distinct()
            .ToListAsync(cancellationToken);

        if (adminEmails.Count == 0) return;

        foreach (var inspection in unresolvedInspections)
        {
            if (string.IsNullOrWhiteSpace(inspection.ItemsJson)) continue;

            var items = JsonSerializer.Deserialize<List<DailyInspectionItemDto>>(inspection.ItemsJson, JsonOptions) ?? [];
            var pendingItems = items.Where(i => !i.IsPassed && i.ResolutionStatus != "Repaired" && i.ResolutionStatus != "Replaced").ToList();

            if (pendingItems.Count == 0) continue;

            var cacheKey = $"inspection:reminder:{inspection.Id:N}";
            var hasRecentReminder = await cacheService.ExistsAsync(cacheKey, cancellationToken);

            if (hasRecentReminder)
            {
                // Đã gửi nhắc nhở trong vòng 2 tiếng trước, bỏ qua
                continue;
            }

            // Kiểm tra xem đã trôi qua ít nhất 2 tiếng kể từ lúc nộp biên bản chưa
            if (nowUtc - inspection.InspectionTime < TimeSpan.FromHours(2))
            {
                continue;
            }

            // Gửi email nhắc nhở
            var hasCritical = pendingItems.Any(i => i.PriorityLevel == "Critical");
            var shiftName = inspection.ShiftId.HasValue
                ? FixedShifts.FindById(inspection.ShiftId.Value)?.Name ?? "Ca làm việc"
                : "Ca làm việc";

            var subject = hasCritical
                ? $"[NHẮC NHỞ AN TOÀN HỒ BƠI] Vẫn còn {pendingItems.Count} thiết bị cốt lõi chưa xử lý - Ca {shiftName}"
                : $"[NHẮC NHỞ XỬ LÝ SỰ CỐ] Biên bản kiểm tra thiết bị {shiftName} ngày {today:dd/MM/yyyy}";

            var itemsHtml = string.Join("", pendingItems.Select(item =>
                $"<li style='margin-bottom:6px;'><strong>{item.EquipmentName}</strong> ({item.Category} - {item.Location ?? "Chưa rõ vị trí"}): <span style='color:#dc2626;'>{item.Note ?? "Có vấn đề"}</span></li>"));

            var body = $@"
                <div style='font-family:Arial,sans-serif;max-width:650px;margin:0 auto;border:1px solid #e5e7eb;border-radius:8px;padding:20px;'>
                    <h3 style='color:#dc2626;margin-top:0;'>⚠️ NHẮC NHỞ: SỰ CỐ THIẾT BỊ CHƯA ĐƯỢC XÁC NHẬN XỬ LÝ</h3>
                    <p style='color:#374151;'>
                        Kính gửi Ban Quản lý / Quản trị viên,<br/>
                        Hệ thống ghi nhận biên bản kiểm tra <strong>{shiftName}</strong> ngày <strong>{today:dd/MM/yyyy}</strong> hiện vẫn còn <strong>{pendingItems.Count} thiết bị chưa được khắc phục</strong>:
                    </p>
                    <ul style='color:#374151;line-height:1.6;'>
                        {itemsHtml}
                    </ul>
                    <p style='color:#6b7280;font-size:13px;'>
                        Để đảm bảo an toàn cho khách bơi theo quy chuẩn pháp luật, vui lòng kiểm tra hiện trường, tiến hành sửa chữa hoặc thay thế và xác nhận cập nhật trên hệ thống quản trị.
                    </p>
                    <p style='color:#9ca3af;font-size:11px;border-top:1px solid #f3f4f6;padding-top:10px;'>
                        (Email tự động định kỳ 2 tiếng/lần từ SmartPool Management System)
                    </p>
                </div>";

            try
            {
                await emailService.SendAsync(adminEmails, subject, body, cancellationToken);
                // Lưu cache 2 tiếng (120 phút) để đảm bảo chu kỳ gửi đúng 2 tiếng 1 lần
                await cacheService.SetAsync(cacheKey, nowUtc.ToString("O"), TimeSpan.FromHours(2), cancellationToken);
                _logger.LogInformation("Đã gửi email nhắc nhở định kỳ cho biên bản {InspectionId} tới {Count} Admin.", inspection.Id, adminEmails.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email nhắc nhở định kỳ cho biên bản {InspectionId}.", inspection.Id);
            }
        }
    }
}

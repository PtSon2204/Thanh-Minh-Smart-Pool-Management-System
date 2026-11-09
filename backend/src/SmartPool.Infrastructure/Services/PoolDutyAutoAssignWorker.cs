using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartPool.Infrastructure.Services;

/// <summary>
/// Background worker tự động phân công lịch trực bể & đo nước theo thuật toán xoay vòng cho nhân viên.
/// Tự động quét và đảm bảo lịch trực cho tuần hiện tại và tuần kế tiếp luôn được gán sẵn.
/// </summary>
public sealed class PoolDutyAutoAssignWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PoolDutyAutoAssignWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[PoolDutyAutoAssignWorker] Worker tự động phân công lịch trực bể đã khởi động.");

        // Chờ 10 giây sau khi ứng dụng khởi động rồi chạy lần quét đầu tiên
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var operations = scope.ServiceProvider.GetRequiredService<IWaterQualityOperations>();
                var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
                var timeProvider = scope.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

                await AutoAssignUpcomingWeeksAsync(operations, db, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[PoolDutyAutoAssignWorker] Lỗi xảy ra trong quá trình tự động phân công lịch trực bể.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task AutoAssignUpcomingWeeksAsync(
        IWaterQualityOperations operations,
        SmartPoolDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var diffToMonday = today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1 - (int)today.DayOfWeek;
        var currentMonday = today.AddDays(diffToMonday);
        var nextWeekSunday = currentMonday.AddDays(13); // 2 tuần

        var hasSchedules = await db.PoolDutySchedules
            .AnyAsync(p => p.DutyDate >= currentMonday && p.DutyDate <= nextWeekSunday, cancellationToken);

        if (!hasSchedules)
        {
            logger.LogInformation("[PoolDutyAutoAssignWorker] Phát hiện chưa có lịch trực từ {FromDate} đến {ToDate}. Đang tự động phân công...", currentMonday, nextWeekSunday);

            var command = new AutoAssignPoolDutyCommand
            {
                FromDate = currentMonday,
                ToDate = nextWeekSunday,
                OverwriteExisting = false,
                DefaultNotes = "Đo pH, Clo tự do đầu ca và kiểm tra vệ sinh mặt nước bể bơi"
            };

            var result = await operations.AutoAssignPoolDutySchedulesAsync(command, cancellationToken);
            logger.LogInformation("[PoolDutyAutoAssignWorker] Hoàn tất tự động phân công: {Message}", result.Message);
        }
    }
}

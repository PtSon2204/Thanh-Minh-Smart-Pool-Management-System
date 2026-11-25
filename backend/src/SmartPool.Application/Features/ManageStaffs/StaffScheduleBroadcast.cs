using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageStaffs;

internal static class StaffScheduleBroadcast
{
    public static async Task NotifyWaterQualityDutyChangedAsync(
        INotificationService? notificationService,
        string eventType,
        CancellationToken cancellationToken)
    {
        if (notificationService is null) return;
        try
        {
            await notificationService.SendToAllAsync("WaterQualityUpdated", new
            {
                type = eventType,
                updatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        catch
        {
            // Không làm gián đoạn thao tác phân công lịch làm việc nếu SignalR lỗi
        }
    }
}

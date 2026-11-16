using SmartPool.Application.Features.ManageEquipments.Cameras;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public static class LiveCameraSelection
{
    public static Guid[] Select(IEnumerable<(Guid Id, CameraConnectionStatus Status)> cameras,
        IReadOnlySet<Guid> running, int capacity, DateTimeOffset now, TimeSpan reconnectGrace) => cameras
        .Where(x => x.Status.State == "Connected" || running.Contains(x.Id) && x.Status.State != "Error" &&
            x.Status.LastFrameAt.HasValue && now - x.Status.LastFrameAt.Value <= reconnectGrace)
        .OrderByDescending(x => x.Status.State == "Connected")
        .ThenByDescending(x => running.Contains(x.Id))
        .ThenBy(x => x.Id)
        .Take(Math.Clamp(capacity, 1, 8)).Select(x => x.Id).ToArray();
}

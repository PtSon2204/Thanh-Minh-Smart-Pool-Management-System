using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IDailyInspectionOperations
{
    Task<DailyInspection?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<DailyInspection?> GetByDateAndShiftAsync(DateOnly date, Guid shiftId, CancellationToken cancellationToken);
    Task<List<DailyInspection>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);
    Task SubmitAsync(DailyInspection inspection, IReadOnlyCollection<Guid> failedEquipmentIds,
        IReadOnlyCollection<Notification> notifications, OperationalTaskExecutionAuthorization? taskExecution,
        CancellationToken cancellationToken);
    Task UpdateAsync(DailyInspection inspection, CancellationToken cancellationToken);
    Task<(List<DailyInspection> Items, int TotalCount)> GetPagedInspectionsAsync(
        Guid? inspectionId,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? overallStatus,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
}

using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IDailyInspectionOperations
{
    Task<DailyInspection?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<DailyInspection?> GetByDateAndShiftAsync(DateOnly date, Guid shiftId, CancellationToken cancellationToken);
    Task<List<DailyInspection>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);
    Task AddAsync(DailyInspection inspection, CancellationToken cancellationToken);
    Task UpdateAsync(DailyInspection inspection, CancellationToken cancellationToken);
    Task<(List<DailyInspection> Items, int TotalCount)> GetPagedInspectionsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        string? overallStatus,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
}

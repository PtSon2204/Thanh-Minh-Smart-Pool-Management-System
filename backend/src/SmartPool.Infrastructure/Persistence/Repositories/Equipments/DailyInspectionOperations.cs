using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Equipments;

public sealed class DailyInspectionOperations(SmartPoolDbContext db, IWaterQualityOperations waterQualityOperations)
    : IDailyInspectionOperations
{
    public async Task<DailyInspection?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.DailyInspections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<DailyInspection?> GetByDateAndShiftAsync(DateOnly date, Guid shiftId, CancellationToken cancellationToken)
    {
        return await db.DailyInspections
            .FirstOrDefaultAsync(x => x.InspectionDate == date && x.ShiftId == shiftId, cancellationToken);
    }

    public async Task<List<DailyInspection>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        return await db.DailyInspections.AsNoTracking()
            .Where(x => x.InspectionDate == date)
            .ToListAsync(cancellationToken);
    }

    public async Task SubmitAsync(DailyInspection inspection, IReadOnlyCollection<Guid> failedEquipmentIds,
        IReadOnlyCollection<Notification> notifications, OperationalTaskExecutionAuthorization? taskExecution,
        CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await db.DailyInspections.AddAsync(inspection, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (inspection.OperationalTaskId.HasValue)
            await waterQualityOperations.CompleteOperationalChecklistTaskAsync(inspection,
                taskExecution ?? throw new InvalidOperationException("Thiếu xác nhận thực hiện công việc kiểm tra thiết bị."),
                cancellationToken);

        if (failedEquipmentIds.Count > 0)
        {
            var equipments = await db.Equipments
                .Where(equipment => failedEquipmentIds.Contains(equipment.Id))
                .ToListAsync(cancellationToken);
            foreach (var equipment in equipments)
            {
                equipment.Status = EquipmentStatuses.Maintenance;
                equipment.UpdatedAt = inspection.InspectionTime;
            }
        }

        await db.Notifications.AddRangeAsync(notifications, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(DailyInspection inspection, CancellationToken cancellationToken)
    {
        db.DailyInspections.Update(inspection);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<DailyInspection> Items, int TotalCount)> GetPagedInspectionsAsync(
        Guid? inspectionId,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? overallStatus,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.DailyInspections.AsNoTracking().AsQueryable();

        if (inspectionId.HasValue)
            query = query.Where(x => x.Id == inspectionId.Value);

        if (fromDate.HasValue)
            query = query.Where(x => x.InspectionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.InspectionDate <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(overallStatus))
            query = query.Where(x => x.OverallStatus == overallStatus);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.InspectionDate)
            .ThenByDescending(x => x.InspectionTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}

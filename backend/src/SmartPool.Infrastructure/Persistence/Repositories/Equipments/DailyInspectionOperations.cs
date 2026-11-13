using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Equipments;

public sealed class DailyInspectionOperations(SmartPoolDbContext db) : IDailyInspectionOperations
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

    public async Task AddAsync(DailyInspection inspection, CancellationToken cancellationToken)
    {
        await db.DailyInspections.AddAsync(inspection, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DailyInspection inspection, CancellationToken cancellationToken)
    {
        db.DailyInspections.Update(inspection);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<DailyInspection> Items, int TotalCount)> GetPagedInspectionsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        string? overallStatus,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.DailyInspections.AsNoTracking().AsQueryable();

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

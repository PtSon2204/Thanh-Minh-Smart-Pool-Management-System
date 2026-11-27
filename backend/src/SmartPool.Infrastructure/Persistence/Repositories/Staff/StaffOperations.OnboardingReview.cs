using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    private async Task<IReadOnlyList<ContractShiftView>> ContractShiftsAsync(ContractTerms? contract, CancellationToken cancellationToken)
    {
        var ids = contract?.WeeklyPattern.SelectMany(item => item.ShiftIds).Distinct().ToArray() ?? [];
        return await context.Shifts.AsNoTracking().Where(item => ids.Contains(item.Id)).OrderBy(item => item.StartTime)
            .Select(item => new ContractShiftView(item.Id, item.Name, item.StartTime, item.EndTime, item.IsActive == true))
            .ToListAsync(cancellationToken);
    }
}

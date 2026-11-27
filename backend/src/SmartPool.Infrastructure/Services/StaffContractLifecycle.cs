using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Services;

public sealed class StaffContractLifecycle(SmartPoolDbContext context,
    IStaffContractAccessPolicy contractAccess, TimeProvider timeProvider)
{
    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var employees = await context.Employees.AsNoTracking()
            .Where(item => !item.ManualAccessEnabled)
            .Where(item => item.Status == "Working" || item.Status == "Inactive" && item.ContractInactiveSinceUtc != null)
            .Where(item => item.User.Role != null && item.User.Role.IsDeleted != true &&
                (EF.Functions.ILike(item.User.Role.Name, "SALES") ||
                 EF.Functions.ILike(item.User.Role.Name, "LIFEGUARD") ||
                 EF.Functions.ILike(item.User.Role.Name, "COACH")))
            .Select(item => item.UserId).ToListAsync(cancellationToken);
        foreach (var employeeId in employees)
            await SynchronizeEmployeeAsync(employeeId, cancellationToken);
    }

    public async Task SynchronizeEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({employeeId.ToString()}, 0))", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.users WHERE id = {employeeId} FOR UPDATE", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT user_id FROM smart_pool.employees WHERE user_id = {employeeId} FOR UPDATE", cancellationToken);
        var employee = await context.Employees.AsNoTracking().Include(item => item.User.Role)
            .SingleOrDefaultAsync(item => item.UserId == employeeId, cancellationToken);
        if (employee?.User.Role is null || employee.User.Role.IsDeleted == true) return;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var current = context.Employees.Where(item => item.UserId == employeeId &&
            item.Status == employee.Status && item.ContractInactiveSinceUtc == employee.ContractInactiveSinceUtc &&
            item.UpdatedAt == employee.UpdatedAt);
        var decision = await contractAccess.EvaluateAsync(employeeId, employee.User.Role.Name, cancellationToken);
        if (!decision.HasContract) return;
        // Forced password setup may continue only while the contract has not
        // started (plan §4.2 pre-contract exception). Every other contract state
        // keeps the normal lifecycle, and only locks this service owns are undone.
        if (decision.State == StaffContractState.NotStarted &&
            await context.Users.AsNoTracking().Where(item => item.Id == employeeId)
                .Select(item => item.MustChangePassword).SingleOrDefaultAsync(cancellationToken))
        {
            if (employee.Status == "Inactive" && employee.ContractInactiveSinceUtc.HasValue)
            {
                await current.ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, "Working")
                    .SetProperty(item => item.ContractInactiveSinceUtc, (DateTime?)null)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);
                await context.Users.Where(item => item.Id == employeeId && item.Status != null &&
                    EF.Functions.ILike(item.Status, "LOCKED") && item.IsDeleted != true)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "ACTIVE")
                        .SetProperty(item => item.UpdatedAt, now), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            return;
        }
        if (employee.Status == "Working" && decision.State != StaffContractState.Active)
        {
            var contractOwnsAccountLock = employee.User.IsDeleted != true &&
                string.Equals(employee.User.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            await current.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, "Inactive")
                .SetProperty(item => item.ContractInactiveSinceUtc, contractOwnsAccountLock ? now : (DateTime?)null)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            if (contractOwnsAccountLock)
                await context.Users.Where(item => item.Id == employeeId && item.Status != null &&
                    EF.Functions.ILike(item.Status, "ACTIVE") && item.IsDeleted != true)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "LOCKED")
                        .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        }
        else if (employee.Status == "Inactive" && employee.ContractInactiveSinceUtc.HasValue &&
            decision.State == StaffContractState.Active && employee.User.IsDeleted != true &&
            string.Equals(employee.User.Status, "LOCKED", StringComparison.OrdinalIgnoreCase))
        {
            await current.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, "Working")
                .SetProperty(item => item.ContractInactiveSinceUtc, (DateTime?)null)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            await context.Users.Where(item => item.Id == employeeId && item.Status != null &&
                EF.Functions.ILike(item.Status, "LOCKED") && item.IsDeleted != true)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "ACTIVE")
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}

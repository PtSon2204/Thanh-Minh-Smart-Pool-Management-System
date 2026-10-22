using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Seeding;

public static class ShiftSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(ShiftSeeder));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(617203041)", cancellationToken);
        var shifts = await db.Shifts.ToListAsync(cancellationToken);
        var fixedIds = FixedShifts.All.Select(shift => shift.Id).ToHashSet();
        var changed = false;

        foreach (var definition in FixedShifts.All)
        {
            var shift = shifts.SingleOrDefault(item => item.Id == definition.Id);
            if (shift is null)
            {
                db.Shifts.Add(new Shift
                {
                    Id = definition.Id,
                    Name = definition.Name,
                    StartTime = definition.StartTime,
                    EndTime = definition.EndTime,
                    IsActive = true
                });
                changed = true;
                continue;
            }

            if (shift.Name != definition.Name)
            {
                shift.Name = definition.Name;
                changed = true;
            }

            if (shift.StartTime != definition.StartTime)
            {
                shift.StartTime = definition.StartTime;
                changed = true;
            }

            if (shift.EndTime != definition.EndTime)
            {
                shift.EndTime = definition.EndTime;
                changed = true;
            }

            if (shift.IsActive != true)
            {
                shift.IsActive = true;
                changed = true;
            }
        }

        foreach (var shift in shifts.Where(shift => !fixedIds.Contains(shift.Id) && shift.IsActive != false))
        {
            shift.IsActive = false;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "[ShiftSeeder] Fixed shifts are synchronized. Changed: {Changed}.",
            changed);
    }
}

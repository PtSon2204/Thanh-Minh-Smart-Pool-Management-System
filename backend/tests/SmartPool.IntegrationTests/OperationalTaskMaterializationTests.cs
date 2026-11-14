using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.Repositories.WaterQuality;

namespace SmartPool.IntegrationTests;

public sealed class OperationalTaskMaterializationTests
{
    [Fact]
    public async Task Ensure_week_keeps_anchor_cursor_and_materializes_distant_then_near_occurrences()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var context = fixture.Context;
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var rangeStart = fixture.Today.AddDays(28 + Random.Shared.Next(0, 3650));
        var anchor = rangeStart.AddDays(-((int)rangeStart.DayOfWeek + 6) % 7);
        var distantWeek = anchor.AddDays(21);
        var lifeguardRoleId = await context.Roles.AsNoTracking()
            .Where(item => item.Name.ToUpper() == "LIFEGUARD" && item.IsDeleted != true)
            .Select(item => item.Id).FirstAsync();
        var lifeguardId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Id = lifeguardId,
            Username = $"materialize-{suffix}",
            Email = $"materialize-{suffix}@example.test",
            Phone = $"09{Random.Shared.Next(10_000_000, 99_999_999)}",
            RoleId = lifeguardRoleId,
            Status = "Active",
            IsDeleted = false,
            PasswordHash = "synthetic-test-only",
            CreatedAt = now,
            UpdatedAt = now
        };
        var employee = new Employee
        {
            UserId = lifeguardId,
            User = user,
            JoinDate = anchor,
            Status = "Working",
            CreatedAt = now,
            UpdatedAt = now
        };
        var recurrence = new PoolDutyMeasurementRecurrence
        {
            Id = Guid.NewGuid(),
            AnchorDate = anchor,
            NextOccurrenceDate = anchor,
            ScheduledTime = new TimeOnly(9, 0),
            ShiftId = fixture.MorningShiftId,
            PreferredEmployeeId = lifeguardId,
            IsActive = true,
            CreatedBy = fixture.AdminId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        context.Users.Add(user);
        context.Employees.Add(employee);
        context.PoolDutyMeasurementRecurrences.Add(recurrence);
        await context.SaveChangesAsync();

        try
        {
            var operations = new WaterQualityOperations(context, fixture.Clock);
            await operations.EnsureOperationalTaskWeekAsync(
                new EnsureOperationalTaskWeekCommand(distantWeek.AddDays(4)) { ActorId = fixture.AdminId }, default);
            await operations.EnsureOperationalTaskWeekAsync(
                new EnsureOperationalTaskWeekCommand(anchor.AddDays(4)) { ActorId = fixture.AdminId }, default);
            var repeatedEnsureCount = await operations.EnsureOperationalTaskWeekAsync(
                new EnsureOperationalTaskWeekCommand(anchor.AddDays(5)) { ActorId = fixture.AdminId }, default);

            var cursor = await context.PoolDutyMeasurementRecurrences.AsNoTracking()
                .Where(item => item.Id == recurrence.Id).Select(item => item.NextOccurrenceDate).SingleAsync();
            var occurrenceDates = await context.PoolDutySchedules.AsNoTracking()
                .Where(item => item.MeasurementRecurrenceId == recurrence.Id)
                .OrderBy(item => item.DutyDate).Select(item => item.DutyDate).ToListAsync();
            var checklistCount = await context.PoolDutySchedules.AsNoTracking()
                .CountAsync(item => item.TaskType == PoolDutyTaskTypes.EquipmentChecklist &&
                    ((item.DutyDate >= anchor && item.DutyDate <= anchor.AddDays(6)) ||
                     (item.DutyDate >= distantWeek && item.DutyDate <= distantWeek.AddDays(6))));

            Assert.Equal(anchor, cursor);
            Assert.Equal([anchor, anchor.AddDays(3), anchor.AddDays(6), distantWeek, distantWeek.AddDays(3), distantWeek.AddDays(6)], occurrenceDates);
            Assert.Equal(28, checklistCount);
            Assert.Equal(0, repeatedEnsureCount);
        }
        finally
        {
            var taskRows = await context.PoolDutySchedules.Where(item =>
                item.MeasurementRecurrenceId == recurrence.Id ||
                (item.TaskType == PoolDutyTaskTypes.EquipmentChecklist &&
                 ((item.DutyDate >= anchor && item.DutyDate <= anchor.AddDays(6)) ||
                  (item.DutyDate >= distantWeek && item.DutyDate <= distantWeek.AddDays(6)))))
                .ToListAsync();
            context.PoolDutySchedules.RemoveRange(taskRows);
            await context.SaveChangesAsync();
            context.PoolDutyMeasurementRecurrences.Remove(recurrence);
            context.Employees.Remove(employee);
            context.Users.Remove(user);
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Due_notice_is_queued_once_and_cancelled_task_is_skipped()
    {
        var dutyDate = new DateOnly(1900, 1, 1).AddDays(Random.Shared.Next(1, 365));
        var utcNow = new DateTimeOffset(dutyDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync(utcNow);
        var context = fixture.Context;
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var taskIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var tasks = new[]
        {
            new PoolDutySchedule
            {
                Id = taskIds[0], TaskType = PoolDutyTaskTypes.EquipmentChecklist,
                InspectionWindow = PoolDutyInspectionWindows.Morning, DutyDate = fixture.Today,
                ScheduledAtUtc = now.AddHours(-4), DueAtUtc = now.AddHours(-1),
                Status = "Unassigned", UnassignedReason = "ADMIN_ASSIGNMENT_REQUIRED", Revision = 1,
                CreatedBy = fixture.AdminId, CreatedAt = now, UpdatedAt = now
            },
            new PoolDutySchedule
            {
                Id = taskIds[1], TaskType = PoolDutyTaskTypes.EquipmentChecklist,
                InspectionWindow = PoolDutyInspectionWindows.Afternoon, DutyDate = fixture.Today,
                ScheduledAtUtc = now.AddHours(-1), DueAtUtc = now.AddHours(-1),
                Status = "Unassigned", UnassignedReason = "ADMIN_ASSIGNMENT_REQUIRED", Revision = 1,
                CreatedBy = fixture.AdminId, CreatedAt = now, UpdatedAt = now
            },
            new PoolDutySchedule
            {
                Id = taskIds[2], TaskType = PoolDutyTaskTypes.Measurement, DutyDate = fixture.Today,
                ScheduledAtUtc = now.AddHours(-2), Status = "Cancelled", Revision = 1,
                CreatedBy = fixture.AdminId, CreatedAt = now, UpdatedAt = now
            }
        };
        context.PoolDutySchedules.AddRange(tasks);
        await context.SaveChangesAsync();

        try
        {
            var operations = new WaterQualityOperations(context, fixture.Clock);
            await operations.MaterializeDueOperationalTasksAsync(default);
            var dueDeliveries = await context.StaffScheduleDeliveries.AsNoTracking()
                .CountAsync(item => taskIds.Take(2).Contains(item.OperationId) && item.Channel == "EMAIL");
            var cancelledDeliveries = await context.StaffScheduleDeliveries.AsNoTracking()
                .CountAsync(item => item.OperationId == taskIds[2] && item.Channel == "EMAIL");
            Assert.All(tasks.Take(2), item => Assert.NotNull(item.DueNoticeSentAtUtc));
            Assert.Null(tasks[2].DueNoticeSentAtUtc);
            Assert.True(dueDeliveries > 0);
            Assert.Equal(0, cancelledDeliveries);

            await operations.MaterializeDueOperationalTasksAsync(default);
            var repeatedDueDeliveries = await context.StaffScheduleDeliveries.AsNoTracking()
                .CountAsync(item => taskIds.Take(2).Contains(item.OperationId) && item.Channel == "EMAIL");
            Assert.Equal(dueDeliveries, repeatedDueDeliveries);
        }
        finally
        {
            var deliveries = await context.StaffScheduleDeliveries
                .Where(item => taskIds.Contains(item.OperationId)).ToListAsync();
            context.StaffScheduleDeliveries.RemoveRange(deliveries);
            var notifications = await context.Notifications.Where(item =>
                item.Type == "STAFF_TASK" && item.CreatedAt == now).ToListAsync();
            context.Notifications.RemoveRange(notifications);
            context.PoolDutySchedules.RemoveRange(tasks);
            await context.SaveChangesAsync();
        }
    }
}

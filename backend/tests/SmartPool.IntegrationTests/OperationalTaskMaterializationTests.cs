using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.Repositories.WaterQuality;
using SmartPool.IntegrationTests.ManageStaffs;

namespace SmartPool.IntegrationTests;

public sealed class OperationalTaskMaterializationTests
{
    [Fact]
    public async Task Ensure_week_bounds_measurements_and_replays_configuration_without_reviving_cancelled_tasks()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        fixture.Clock.Now = fixture.Clock.Now.AddDays(4);
        var context = fixture.Context;
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var monday = fixture.Today.AddDays(28);
        monday = monday.AddDays(-((int)monday.DayOfWeek + 6) % 7);
        var anchor = monday;
        var thursday = anchor.AddDays(3);
        var sunday = anchor.AddDays(6);
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
            JoinDate = fixture.Today,
            Status = "Working",
            CreatedAt = now,
            UpdatedAt = now
        };
        var cancelledLegacyTask = new PoolDutySchedule
        {
            Id = Guid.NewGuid(),
            TaskType = PoolDutyTaskTypes.Measurement,
            DutyDate = thursday,
            ShiftId = fixture.MorningShiftId,
            ScheduledAtUtc = new DateTimeOffset(thursday.ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromHours(7)).UtcDateTime,
            Status = "Cancelled",
            Revision = 2,
            PreferredEmployeeId = lifeguardId,
            CreatedBy = fixture.AdminId,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.Users.Add(user);
        context.Employees.Add(employee);
        context.PoolDutySchedules.Add(cancelledLegacyTask);
        await context.SaveChangesAsync();

        try
        {
            var operations = new WaterQualityOperations(context, fixture.Clock);
            var currentMonday = fixture.Today.AddDays(-((int)fixture.Today.DayOfWeek + 6) % 7);
            var pastAnchorRequest = new EnsureOperationalTaskWeekCommand(currentMonday, 1, new TimeOnly(9, 0),
                fixture.MorningShiftId, lifeguardId) { ActorId = fixture.AdminId };
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                operations.EnsureOperationalTaskWeekAsync(pastAnchorRequest, default));

            var request = new EnsureOperationalTaskWeekCommand(monday, 1, new TimeOnly(9, 0),
                fixture.MorningShiftId, lifeguardId) { ActorId = fixture.AdminId };
            var first = await operations.EnsureOperationalTaskWeekAsync(request, default);
            fixture.Clock.Now = monday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(7).AddDays(1);
            var repeated = await operations.EnsureOperationalTaskWeekAsync(request with { WeekStart = monday.AddDays(2) }, default);
            var checklistCount = await context.PoolDutySchedules.AsNoTracking()
                .CountAsync(item => item.TaskType == PoolDutyTaskTypes.EquipmentChecklist &&
                    item.DutyDate >= anchor && item.DutyDate <= sunday);
            var measurementTasks = await context.PoolDutySchedules.AsNoTracking()
                .Where(item => item.TaskType == PoolDutyTaskTypes.Measurement &&
                    item.MeasurementRecurrenceId == first.Configuration.Id && item.DutyDate >= anchor && item.DutyDate <= sunday)
                .OrderBy(item => item.DutyDate).ToListAsync();

            Assert.Equal(16, first.CreatedCount);
            Assert.Equal(1, first.ExistingCount);
            Assert.Equal(2, first.UnassignedCount);
            Assert.Equal(monday, first.Configuration.WeekStart);
            Assert.Equal(1, first.Configuration.StartDayOfWeek);
            Assert.Equal(14, checklistCount);
            Assert.Equal([anchor, sunday], measurementTasks.Select(item => item.DutyDate));
            Assert.All(measurementTasks, item => Assert.Equal("Unassigned", item.Status));
            Assert.Equal("Cancelled", await context.PoolDutySchedules.AsNoTracking()
                .Where(item => item.Id == cancelledLegacyTask.Id).Select(item => item.Status).SingleAsync());
            Assert.Equal(0, repeated.CreatedCount);
            Assert.Equal(17, repeated.ExistingCount);
            await Assert.ThrowsAsync<SmartPool.Application.Features.ManageStaffs.StaffConflictException>(() =>
                operations.EnsureOperationalTaskWeekAsync(request with { ScheduledTime = new TimeOnly(9, 15) }, default));
            Assert.Equal(1, await context.PoolDutyMeasurementRecurrences.AsNoTracking()
                .CountAsync(item => item.WeekStart == monday));
            Assert.Equal(17, await context.PoolDutySchedules.AsNoTracking()
                .CountAsync(item => item.TaskType != null && item.DutyDate >= anchor && item.DutyDate <= sunday));
        }
        finally
        {
            var configuration = await context.PoolDutyMeasurementRecurrences
                .SingleOrDefaultAsync(item => item.WeekStart == monday);
            var configurationId = configuration?.Id;
            var taskRows = await context.PoolDutySchedules.Where(item =>
                (configurationId.HasValue && item.MeasurementRecurrenceId == configurationId.Value) ||
                item.Id == cancelledLegacyTask.Id ||
                (item.TaskType == PoolDutyTaskTypes.EquipmentChecklist && item.DutyDate >= anchor && item.DutyDate <= sunday))
                .ToListAsync();
            context.PoolDutySchedules.RemoveRange(taskRows);
            await context.SaveChangesAsync();
            if (configuration is not null) context.PoolDutyMeasurementRecurrences.Remove(configuration);
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

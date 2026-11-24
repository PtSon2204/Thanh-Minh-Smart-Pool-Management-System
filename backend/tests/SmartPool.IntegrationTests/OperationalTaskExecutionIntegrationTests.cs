using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.Repositories.WaterQuality;
using SmartPool.IntegrationTests.ManageStaffs;
using SmartPool.Domain.Constants;

namespace SmartPool.IntegrationTests;

public sealed class OperationalTaskExecutionIntegrationTests
{
    [Fact]
    public async Task Confirmed_admin_completes_unassigned_measurement_and_records_actor_audit()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var context = fixture.Context;
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var task = CreateUnassignedMeasurementTask(fixture, now);
        var log = CreateLog(task.Id, fixture.AdminId, now, "WithinLimits");
        context.PoolDutySchedules.Add(task);
        await context.SaveChangesAsync();

        try
        {
            var operations = new WaterQualityOperations(context, fixture.Clock);
            await operations.CreateWaterQualityLogAsync(log, default,
                new OperationalTaskExecutionAuthorization(true, false, true, task.Revision, null));

            var savedTask = await context.PoolDutySchedules.AsNoTracking().SingleAsync(item => item.Id == task.Id);
            var savedLog = await context.WaterQualityLogs.AsNoTracking().SingleAsync(item => item.Id == log.Id);
            Assert.Equal("Completed", savedTask.Status);
            Assert.Null(savedTask.AssignedEmployeeId);
            Assert.Equal(2, savedTask.Revision);
            Assert.Equal(fixture.AdminId, savedLog.InspectedBy);
            Assert.Equal(fixture.AdminId, savedTask.LastExecutedBy);
            Assert.Null(savedTask.LastExecutionAssigneeId);
            Assert.True(savedTask.LastExecutionWasAdminOverride);
            Assert.Equal(log.Id, savedTask.WaterQualityLogId);
        }
        finally
        {
            context.ChangeTracker.Clear();
            var logs = await context.WaterQualityLogs.Where(item => item.Id == log.Id).ToListAsync();
            var savedTask = await context.PoolDutySchedules.SingleAsync(item => item.Id == task.Id);
            context.WaterQualityLogs.RemoveRange(logs);
            await context.SaveChangesAsync();
            context.PoolDutySchedules.Remove(savedTask);
            await context.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData(false, 1L)]
    [InlineData(true, 0L)]
    public async Task Admin_cannot_execute_without_confirmation_or_with_stale_revision(
        bool confirmed, long expectedRevision)
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var context = fixture.Context;
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var task = CreateUnassignedMeasurementTask(fixture, now);
        task.Revision = 2;
        context.PoolDutySchedules.Add(task);
        await context.SaveChangesAsync();

        try
        {
            var operations = new WaterQualityOperations(context, fixture.Clock);
            await Assert.ThrowsAsync<InvalidOperationException>(() => operations.CreateWaterQualityLogAsync(
                CreateLog(task.Id, fixture.AdminId, now, "WithinLimits"), default,
                new OperationalTaskExecutionAuthorization(true, false, confirmed, expectedRevision, null)));
            Assert.False(await context.WaterQualityLogs.AnyAsync(item => item.OperationalTaskId == task.Id));
            var savedTask = await context.PoolDutySchedules.AsNoTracking().SingleAsync(item => item.Id == task.Id);
            Assert.Null(savedTask.LastExecutedBy);
        }
        finally
        {
            context.ChangeTracker.Clear();
            var savedTask = await context.PoolDutySchedules.SingleAsync(item => item.Id == task.Id);
            context.PoolDutySchedules.Remove(savedTask);
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Admin_unsafe_measurement_keeps_original_assignee_on_cleaning_task()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var context = fixture.Context;
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var lifeguardRoleId = await context.Roles.AsNoTracking()
            .Where(item => item.Name.ToUpper() == "LIFEGUARD" && item.IsDeleted != true)
            .Select(item => item.Id).FirstAsync();
        var lifeguardId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var lifeguard = new User
        {
            Id = lifeguardId,
            Username = $"task-execution-{suffix}",
            Email = $"task-execution-{suffix}@example.test",
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
            User = lifeguard,
            JoinDate = fixture.Today,
            Status = "Working",
            CreatedAt = now,
            UpdatedAt = now
        };
        var assignment = new EmployeeShift
        {
            Id = Guid.NewGuid(),
            EmployeeId = lifeguardId,
            ShiftId = fixture.AfternoonShiftId,
            WorkDate = fixture.Today,
            Status = "Scheduled",
            CreatedAt = now,
            UpdatedAt = now
        };
        var task = CreateUnassignedMeasurementTask(fixture, now);
        task.AssignedEmployeeId = lifeguardId;
        task.PreferredEmployeeId = lifeguardId;
        task.EmployeeShiftId = assignment.Id;
        task.ShiftId = fixture.AfternoonShiftId;
        task.Status = "Scheduled";
        task.Revision = 3;
        var log = CreateLog(task.Id, fixture.AdminId, now, "Warning");
        context.Users.Add(lifeguard);
        context.Employees.Add(employee);
        context.EmployeeShifts.Add(assignment);
        context.PoolDutySchedules.Add(task);
        await context.SaveChangesAsync();

        try
        {
            var operations = new WaterQualityOperations(context, fixture.Clock);
            await operations.CreateWaterQualityLogAsync(log, default,
                new OperationalTaskExecutionAuthorization(true, false, true, task.Revision, lifeguardId));

            var savedTask = await context.PoolDutySchedules.AsNoTracking().SingleAsync(item => item.Id == task.Id);
            var followUp = await context.PoolDutySchedules.AsNoTracking()
                .SingleAsync(item => item.TriggerWaterQualityLogId == log.Id);
            Assert.Equal(lifeguardId, savedTask.AssignedEmployeeId);
            Assert.Equal(fixture.AdminId, savedTask.LastExecutedBy);
            Assert.Equal(lifeguardId, savedTask.LastExecutionAssigneeId);
            Assert.True(savedTask.LastExecutionWasAdminOverride);
            Assert.Equal(lifeguardId, followUp.AssignedEmployeeId);
            Assert.Equal(assignment.Id, followUp.EmployeeShiftId);
            Assert.Equal("InProgress", followUp.Status);
        }
        finally
        {
            context.ChangeTracker.Clear();
            var followUps = await context.PoolDutySchedules
                .Where(item => item.TriggerWaterQualityLogId == log.Id).ToListAsync();
            var logs = await context.WaterQualityLogs.Where(item => item.Id == log.Id).ToListAsync();
            var savedTask = await context.PoolDutySchedules.SingleAsync(item => item.Id == task.Id);
            var deliveries = await context.StaffScheduleDeliveries
                .Where(item => followUps.Select(followUp => followUp.Id).Contains(item.OperationId)).ToListAsync();
            var notifications = await context.Notifications.Where(item => item.UserId == lifeguardId).ToListAsync();
            context.StaffScheduleDeliveries.RemoveRange(deliveries);
            context.Notifications.RemoveRange(notifications);
            await context.SaveChangesAsync();
            context.PoolDutySchedules.RemoveRange(followUps);
            await context.SaveChangesAsync();
            context.WaterQualityLogs.RemoveRange(logs);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            var taskToDelete = await context.PoolDutySchedules.SingleAsync(item => item.Id == task.Id);
            var assignmentToDelete = await context.EmployeeShifts.SingleAsync(item => item.Id == assignment.Id);
            var employeeToDelete = await context.Employees.SingleAsync(item => item.UserId == lifeguardId);
            var userToDelete = await context.Users.SingleAsync(item => item.Id == lifeguardId);
            context.PoolDutySchedules.Remove(taskToDelete);
            context.EmployeeShifts.Remove(assignmentToDelete);
            context.Employees.Remove(employeeToDelete);
            context.Users.Remove(userToDelete);
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Confirmed_admin_completes_unassigned_checklist_with_actor_audit()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var context = fixture.Context;
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var date = new DateOnly(2070, 1, 1).AddDays(Random.Shared.Next(0, 365 * 20));
        await EnsureFixedShiftAsync(context, FixedShifts.Morning);
        var task = new PoolDutySchedule
        {
            Id = Guid.NewGuid(),
            TaskType = PoolDutyTaskTypes.EquipmentChecklist,
            InspectionWindow = PoolDutyInspectionWindows.Morning,
            DutyDate = date,
            ScheduledAtUtc = date.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc),
            DueAtUtc = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
            Status = "Unassigned",
            UnassignedReason = "ADMIN_ASSIGNMENT_REQUIRED",
            Revision = 4,
            CreatedBy = fixture.AdminId,
            CreatedAt = now,
            UpdatedAt = now
        };
        var inspection = new DailyInspection
        {
            Id = Guid.NewGuid(),
            InspectorId = fixture.AdminId,
            ShiftId = FixedShifts.Morning.Id,
            OperationalTaskId = task.Id,
            InspectionDate = date,
            InspectionTime = now,
            ItemsJson = "[]",
            OverallStatus = "Passed",
            CreatedAt = now,
            UpdatedAt = now
        };
        context.PoolDutySchedules.Add(task);
        await context.SaveChangesAsync();

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.DailyInspections.Add(inspection);
            var operations = new WaterQualityOperations(context, fixture.Clock);
            await operations.CompleteOperationalChecklistTaskAsync(inspection,
                new OperationalTaskExecutionAuthorization(true, false, true, task.Revision, null), default);
            await transaction.CommitAsync();

            context.ChangeTracker.Clear();
            var savedTask = await context.PoolDutySchedules.AsNoTracking().SingleAsync(item => item.Id == task.Id);
            var savedInspection = await context.DailyInspections.AsNoTracking().SingleAsync(item => item.Id == inspection.Id);
            Assert.Equal("Completed", savedTask.Status);
            Assert.Null(savedTask.AssignedEmployeeId);
            Assert.Equal(5, savedTask.Revision);
            Assert.Equal(fixture.AdminId, savedInspection.InspectorId);
            Assert.Equal(fixture.AdminId, savedTask.LastExecutedBy);
            Assert.Null(savedTask.LastExecutionAssigneeId);
            Assert.True(savedTask.LastExecutionWasAdminOverride);
            Assert.Equal(inspection.Id, savedTask.DailyInspectionId);
        }
        finally
        {
            context.ChangeTracker.Clear();
            var inspections = await context.DailyInspections.Where(item => item.Id == inspection.Id).ToListAsync();
            var savedTask = await context.PoolDutySchedules.SingleAsync(item => item.Id == task.Id);
            savedTask.DailyInspectionId = null;
            await context.SaveChangesAsync();
            context.DailyInspections.RemoveRange(inspections);
            await context.SaveChangesAsync();
            context.PoolDutySchedules.Remove(savedTask);
            await context.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData(false, 4L)]
    [InlineData(true, 3L)]
    public async Task Checklist_rejects_unconfirmed_or_stale_admin_execution(bool confirmed, long expectedRevision)
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var context = fixture.Context;
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var date = new DateOnly(2070, 1, 1).AddDays(Random.Shared.Next(0, 365 * 20));
        await EnsureFixedShiftAsync(context, FixedShifts.Morning);
        var task = new PoolDutySchedule
        {
            Id = Guid.NewGuid(),
            TaskType = PoolDutyTaskTypes.EquipmentChecklist,
            InspectionWindow = PoolDutyInspectionWindows.Morning,
            DutyDate = date,
            ScheduledAtUtc = date.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc),
            DueAtUtc = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
            Status = "Unassigned",
            UnassignedReason = "ADMIN_ASSIGNMENT_REQUIRED",
            Revision = 4,
            CreatedBy = fixture.AdminId,
            CreatedAt = now,
            UpdatedAt = now
        };
        var inspection = new DailyInspection
        {
            Id = Guid.NewGuid(),
            InspectorId = fixture.AdminId,
            ShiftId = FixedShifts.Morning.Id,
            OperationalTaskId = task.Id,
            InspectionDate = date,
            InspectionTime = now,
            ItemsJson = "[]",
            OverallStatus = "Passed",
            CreatedAt = now,
            UpdatedAt = now
        };
        context.PoolDutySchedules.Add(task);
        await context.SaveChangesAsync();

        try
        {
            var operations = new WaterQualityOperations(context, fixture.Clock);
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.DailyInspections.Add(inspection);
            await Assert.ThrowsAsync<InvalidOperationException>(() => operations.CompleteOperationalChecklistTaskAsync(
                inspection, new OperationalTaskExecutionAuthorization(true, false, confirmed, expectedRevision, null), default));
            await transaction.RollbackAsync();
            context.ChangeTracker.Clear();
            Assert.False(await context.DailyInspections.AnyAsync(item => item.Id == inspection.Id));
            var savedTask = await context.PoolDutySchedules.AsNoTracking().SingleAsync(item => item.Id == task.Id);
            Assert.Null(savedTask.LastExecutedBy);
        }
        finally
        {
            context.ChangeTracker.Clear();
            var savedTask = await context.PoolDutySchedules.SingleAsync(item => item.Id == task.Id);
            context.PoolDutySchedules.Remove(savedTask);
            await context.SaveChangesAsync();
        }
    }

    private static PoolDutySchedule CreateUnassignedMeasurementTask(StaffWorkPatternTestFixture fixture, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TaskType = PoolDutyTaskTypes.Measurement,
            DutyDate = fixture.Today,
            ScheduledAtUtc = now.AddMinutes(-1),
            Status = "Unassigned",
            UnassignedReason = "NO_ELIGIBLE_WORK_SHIFT",
            Revision = 1,
            CreatedBy = fixture.AdminId,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static WaterQualityLog CreateLog(Guid taskId, Guid actorId, DateTime now, string status) => new()
    {
        Id = Guid.NewGuid(),
        OperationalTaskId = taskId,
        InspectedBy = actorId,
        InspectionTime = now,
        PhValue = status == "Warning" ? 7.0m : 7.4m,
        FreeChlorineMgL = status == "Warning" ? 0.2m : 1.8m,
        IsWaterClear = true,
        Status = status,
        CreatedAt = now
    };

    private static async Task EnsureFixedShiftAsync(SmartPool.Infrastructure.Persistence.DbContext.SmartPoolDbContext context,
        FixedShiftDefinition definition)
    {
        if (await context.Shifts.AnyAsync(item => item.Id == definition.Id)) return;
        context.Shifts.Add(new Shift
        {
            Id = definition.Id,
            Name = definition.Name,
            StartTime = definition.StartTime,
            EndTime = definition.EndTime,
            IsActive = true
        });
        await context.SaveChangesAsync();
    }
}

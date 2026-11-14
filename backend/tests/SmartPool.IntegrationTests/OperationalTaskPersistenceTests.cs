using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Migrations;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests;

public sealed class OperationalTaskPersistenceTests
{
    [Fact]
    public void Model_protects_result_identity_and_due_notice_concurrency()
    {
        using var context = new SmartPoolDbContext(new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql(IntegrationTestSettings.DefaultConnection).Options);

        var model = context.GetService<IDesignTimeModel>().Model;
        var task = model.FindEntityType(typeof(PoolDutySchedule))!;

        Assert.Null(model.FindEntityType("SmartPool.Domain.Entities.PoolDutyTaskExecution"));
        Assert.True(task.FindProperty(nameof(PoolDutySchedule.DueNoticeSentAtUtc))!.IsNullable);
        Assert.True(task.FindProperty(nameof(PoolDutySchedule.DueNoticeSentAtUtc))!.IsConcurrencyToken);
        Assert.True(task.FindProperty(nameof(PoolDutySchedule.LastExecutedBy))!.IsNullable);
        Assert.True(task.FindProperty(nameof(PoolDutySchedule.LastExecutedAtUtc))!.IsNullable);
        Assert.True(task.FindProperty(nameof(PoolDutySchedule.LastExecutionAssigneeId))!.IsNullable);
        Assert.True(task.FindProperty(nameof(PoolDutySchedule.LastExecutionWasAdminOverride))!.IsNullable);
        Assert.True(task.FindProperty(nameof(PoolDutySchedule.DailyInspectionId))!.IsNullable);
    }

    [Fact]
    public void Migration_only_adds_audit_storage_and_nullable_due_marker()
    {
        Migration migration = new AddOperationalTaskExecutionAudit();
        var operations = migration.UpOperations;

        var column = Assert.Single(operations.OfType<AddColumnOperation>());
        Assert.Equal("due_notice_sent_at_utc", column.Name);
        Assert.True(column.IsNullable);
        Assert.Null(column.DefaultValue);
        Assert.Equal("pool_duty_task_executions", Assert.Single(operations.OfType<CreateTableOperation>()).Name);
        Assert.All(operations, operation => Assert.True(
            operation is AddColumnOperation or CreateTableOperation or CreateIndexOperation));
    }

    [Fact]
    public async Task Persistence_retains_results_and_rolls_back_a_duplicate_due_notice()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var context = fixture.Context;
        await using var transaction = await context.Database.BeginTransactionAsync();
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var task = new PoolDutySchedule
        {
            Id = Guid.NewGuid(), TaskType = PoolDutyTaskTypes.Measurement,
            DutyDate = fixture.Today, Status = "Unassigned", Revision = 1,
            CreatedAt = now, UpdatedAt = now
        };
        context.PoolDutySchedules.Add(task);
        await context.SaveChangesAsync();
        Assert.Null(task.DueNoticeSentAtUtc);

        var log = new WaterQualityLog
        {
            Id = Guid.NewGuid(), OperationalTaskId = task.Id, InspectedBy = fixture.AdminId,
            InspectionTime = now, CreatedAt = now, Status = "Warning"
        };
        var cleaning = new PoolDutySchedule
        {
            Id = Guid.NewGuid(), TaskType = PoolDutyTaskTypes.Cleaning,
            TriggerWaterQualityLogId = log.Id, DutyDate = fixture.Today,
            Status = "Unassigned", Revision = 1, CreatedAt = now, UpdatedAt = now
        };
        context.WaterQualityLogs.Add(log);
        context.PoolDutySchedules.Add(cleaning);
        await context.SaveChangesAsync();
        var recheck = new WaterQualityLog
        {
            Id = Guid.NewGuid(), OperationalTaskId = cleaning.Id, InspectedBy = fixture.AdminId,
            InspectionTime = now, CreatedAt = now, Status = "Danger"
        };
        var earlierRecheck = new WaterQualityLog
        {
            Id = Guid.NewGuid(), OperationalTaskId = cleaning.Id, InspectedBy = fixture.AdminId,
            InspectionTime = now.AddMinutes(-1), CreatedAt = now.AddMinutes(-1), Status = "Warning"
        };
        task.WaterQualityLogId = log.Id;
        cleaning.WaterQualityLogId = recheck.Id;
        context.WaterQualityLogs.Add(recheck);
        context.WaterQualityLogs.Add(earlierRecheck);
        await context.SaveChangesAsync();
        Assert.Equal(1, await context.WaterQualityLogs.CountAsync(item => item.OperationalTaskId == task.Id));
        Assert.Equal(2, await context.WaterQualityLogs.CountAsync(item => item.OperationalTaskId == cleaning.Id));
        Assert.Contains(await context.WaterQualityLogs.AsNoTracking()
            .Where(item => item.OperationalTaskId == cleaning.Id).Select(item => item.Id).ToListAsync(),
            item => item == earlierRecheck.Id);
        Assert.Equal(recheck.Id, cleaning.WaterQualityLogId);
        Assert.Null(task.AssignedEmployeeId);
        Assert.Null(cleaning.AssignedEmployeeId);

        await using var staleContext = new SmartPoolDbContext(new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql(context.Database.GetDbConnection()).Options);
        await staleContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
        var staleTask = await staleContext.PoolDutySchedules.SingleAsync(item => item.Id == task.Id);
        task.DueNoticeSentAtUtc = now;
        var delivery = new StaffScheduleDelivery
        {
            OperationId = task.Id, RecipientId = fixture.AdminId,
            RecipientEmail = "task-audit@example.test", Subject = "Synthetic due notice",
            HtmlBody = "Synthetic due notice", CreatedAtUtc = now
        };
        context.StaffScheduleDeliveries.Add(delivery);
        await context.SaveChangesAsync();
        staleTask.DueNoticeSentAtUtc = now;
        var duplicateDelivery = new StaffScheduleDelivery
        {
            OperationId = Guid.NewGuid(), RecipientId = fixture.AdminId,
            RecipientEmail = "task-audit@example.test", Subject = "Synthetic stale due notice",
            HtmlBody = "Synthetic stale due notice", CreatedAtUtc = now
        };
        staleContext.StaffScheduleDeliveries.Add(duplicateDelivery);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());
        Assert.True(await context.StaffScheduleDeliveries.AnyAsync(item => item.Id == delivery.Id));
        Assert.False(await context.StaffScheduleDeliveries.AnyAsync(item => item.Id == duplicateDelivery.Id));
        await transaction.RollbackAsync();
    }
}

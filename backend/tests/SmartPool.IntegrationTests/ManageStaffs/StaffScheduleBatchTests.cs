using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageStaffs;

namespace SmartPool.IntegrationTests.ManageStaffs;

public sealed partial class StaffScheduleBatchTests
{
    private static readonly DateTimeOffset AssignmentNow = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GivenEligibleEmployee_WhenAssigningSingleShift_ThenAssignmentIsCreated()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var shift = CreateShift(new TimeOnly(8, 0), new TimeOnly(12, 0));
        var workDate = new DateOnly(2026, 11, 3);
        try
        {
            await AddShiftsAsync(fixture, shift);

            var result = await fixture.CreateOperations(timeProvider: new FixedTimeProvider(AssignmentNow)).AssignShiftAsync(
                Single(fixture.AttachedStaffUserId, shift.Id, workDate), CancellationToken.None);

            Assert.Equal((fixture.AttachedStaffUserId, shift.Id), (result.EmployeeId, result.ShiftId));
            await using var readContext = fixture.CreateContext();
            Assert.True(await readContext.EmployeeShifts.AnyAsync(item => item.Id == result.Id));
        }
        finally
        {
            await CleanupScheduleAsync(fixture, [shift.Id], 1);
        }
    }

    [Fact]
    public async Task GivenSavedOverlap_WhenAssigningSingleShift_ThenAssignmentIsRejected()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var savedShift = CreateShift(new TimeOnly(22, 0), new TimeOnly(6, 0));
        var requestedShift = CreateShift(new TimeOnly(5, 0), new TimeOnly(9, 0));
        var savedDate = new DateOnly(2026, 11, 4);
        try
        {
            await AddScheduleAsync(fixture, [savedShift, requestedShift],
                [CreateAssignment(fixture.AttachedStaffUserId, savedShift.Id, savedDate)]);

            await Assert.ThrowsAsync<StaffConflictException>(() => fixture.CreateOperations(timeProvider: new FixedTimeProvider(AssignmentNow)).AssignShiftAsync(
                Single(fixture.AttachedStaffUserId, requestedShift.Id, savedDate.AddDays(1)), CancellationToken.None));

            Assert.Equal(1, await CountAssignmentsAsync(fixture, [savedShift.Id, requestedShift.Id]));
        }
        finally
        {
            await CleanupScheduleAsync(fixture, [savedShift.Id, requestedShift.Id], 1);
        }
    }

    [Fact]
    public async Task GivenCancelledExactTuple_WhenAssigningSingleAndBatch_ThenBothReturnConflict()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var shift = CreateShift(new TimeOnly(8, 0), new TimeOnly(12, 0));
        var workDate = new DateOnly(2026, 11, 7);
        try
        {
            var cancelled = CreateAssignment(fixture.AttachedStaffUserId, shift.Id, workDate);
            cancelled.Status = "Cancelled";
            await AddScheduleAsync(fixture, [shift], [cancelled]);

            await using var singleContext = fixture.CreateContext();
            var singleController = new StaffsController(new OperationsSender(fixture.CreateOperations(singleContext, new FixedTimeProvider(AssignmentNow))));
            var singleResult = await singleController.AssignShift(
                Single(fixture.AttachedStaffUserId, shift.Id, workDate), CancellationToken.None);

            await using var batchContext = fixture.CreateContext();
            var batchController = new StaffsController(new OperationsSender(fixture.CreateOperations(batchContext, new FixedTimeProvider(AssignmentNow))));
            var batchResult = await batchController.AssignShiftBatch(
                Batch(Item(fixture.AttachedStaffUserId, shift.Id, workDate)), CancellationToken.None);

            Assert.IsType<ConflictObjectResult>(singleResult);
            var batchConflict = Assert.IsType<ConflictObjectResult>(batchResult);
            var problem = Assert.IsType<ProblemDetails>(batchConflict.Value);
            Assert.True(problem.Extensions.ContainsKey("conflicts"));
            Assert.Equal(1, await CountAssignmentsAsync(fixture, [shift.Id]));
        }
        finally
        {
            await CleanupScheduleAsync(fixture, [shift.Id], 1);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

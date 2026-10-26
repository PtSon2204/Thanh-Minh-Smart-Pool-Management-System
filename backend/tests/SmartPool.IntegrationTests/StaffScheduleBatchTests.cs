using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch;
using SmartPool.Application.Features.ManageStaffs.Queries.GetScheduleEmployees;

namespace SmartPool.IntegrationTests;

public sealed partial class StaffScheduleBatchTests
{
    [Fact]
    public async Task GivenEligibleEmployee_WhenAssigningSingleShift_ThenAssignmentIsCreated()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var shift = CreateShift(new TimeOnly(8, 0), new TimeOnly(12, 0));
        var workDate = new DateOnly(2026, 11, 2);
        try
        {
            await AddShiftsAsync(fixture, shift);

            var result = await fixture.CreateOperations().AssignShiftAsync(
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

            await Assert.ThrowsAsync<StaffConflictException>(() => fixture.CreateOperations().AssignShiftAsync(
                Single(fixture.AttachedStaffUserId, requestedShift.Id, savedDate.AddDays(1)), CancellationToken.None));

            Assert.Equal(1, await CountAssignmentsAsync(fixture, [savedShift.Id, requestedShift.Id]));
        }
        finally
        {
            await CleanupScheduleAsync(fixture, [savedShift.Id, requestedShift.Id], 1);
        }
    }

    [Fact]
    public async Task GivenTwoEligibleEmployees_WhenAssigningBatch_ThenBothAssignmentsAreCreated()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var shift = CreateShift(new TimeOnly(8, 0), new TimeOnly(12, 0));
        var workDate = new DateOnly(2026, 11, 5);
        try
        {
            await fixture.CreateOperations().CreateStaffAsync(fixture.CreateCommand(fixture.EligibleAdminUserId), CancellationToken.None);
            await AddShiftsAsync(fixture, shift);
            var command = Batch(
                Item(fixture.AttachedStaffUserId, shift.Id, workDate),
                Item(fixture.EligibleAdminUserId, shift.Id, workDate));

            var rows = await fixture.CreateOperations().AssignShiftBatchAsync(command, CancellationToken.None);

            Assert.Equal(2, rows.Count);
            Assert.Equal(2, await CountAssignmentsAsync(fixture, [shift.Id]));
        }
        finally
        {
            await CleanupScheduleAsync(fixture, [shift.Id], 2);
        }
    }

    [Fact]
    public async Task GivenInvalidRows_WhenAssigningBatch_ThenAllConflictsReturnAndStoredRowsStayUnchanged()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var savedShift = CreateShift(new TimeOnly(8, 0), new TimeOnly(12, 0));
        var overlappingShift = CreateShift(new TimeOnly(10, 0), new TimeOnly(14, 0));
        var inactiveShift = CreateShift(new TimeOnly(14, 0), new TimeOnly(18, 0));
        inactiveShift.IsActive = false;
        var workDate = new DateOnly(2026, 11, 6);
        try
        {
            await AddScheduleAsync(fixture, [savedShift, overlappingShift, inactiveShift],
                [CreateAssignment(fixture.AttachedStaffUserId, savedShift.Id, workDate)]);
            var shiftIds = new[] { savedShift.Id, overlappingShift.Id, inactiveShift.Id };
            var before = await ReadAssignmentsAsync(fixture, shiftIds);

            var exception = await Assert.ThrowsAsync<AssignShiftBatchConflictException>(() => fixture.CreateOperations()
                .AssignShiftBatchAsync(Batch(
                    Item(fixture.AttachedStaffUserId, overlappingShift.Id, workDate),
                    Item(fixture.EligibleStaffUserId, savedShift.Id, workDate),
                    Item(fixture.AttachedStaffUserId, inactiveShift.Id, workDate)), CancellationToken.None));

            Assert.Equal(new[] { "employee_ineligible", "overlap", "shift_unavailable" },
                exception.Conflicts.Select(conflict => conflict.Code).Order());
            Assert.Equal(before, await ReadAssignmentsAsync(fixture, shiftIds));
        }
        finally
        {
            await CleanupScheduleAsync(fixture, [savedShift.Id, overlappingShift.Id, inactiveShift.Id], 1);
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
            var singleController = new StaffsController(new OperationsSender(fixture.CreateOperations(singleContext)));
            var singleResult = await singleController.AssignShift(
                Single(fixture.AttachedStaffUserId, shift.Id, workDate), CancellationToken.None);

            await using var batchContext = fixture.CreateContext();
            var batchController = new StaffsController(new OperationsSender(fixture.CreateOperations(batchContext)));
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

    [Fact]
    public async Task GivenMixedEmployees_WhenReadingEligibleProjection_ThenOnlyAssignmentFieldsAndEligibleEmployeesAreReturned()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        try
        {
            await fixture.CreateOperations().CreateStaffAsync(fixture.CreateCommand(fixture.EligibleAdminUserId), CancellationToken.None);
            string fallbackName;
            await using (var setupContext = fixture.CreateContext())
            {
                var fallbackUser = await setupContext.Users.Include(user => user.UserProfile)
                    .SingleAsync(user => user.Id == fixture.EligibleAdminUserId);
                fallbackUser.UserProfile!.FullName = "   ";
                fallbackName = fallbackUser.Username ?? string.Empty;
                await setupContext.SaveChangesAsync();
            }

            var rows = await fixture.CreateOperations().GetScheduleEmployeesAsync(CancellationToken.None);
            var fixtureRows = rows.Where(row => fixture.UserIds.Contains(row.EmployeeId)).ToList();

            Assert.Equal(new[] { fixture.AttachedStaffUserId, fixture.EligibleAdminUserId }.Order(),
                fixtureRows.Select(row => row.EmployeeId).Order());
            Assert.All(fixtureRows, row => Assert.Equal("Working", row.EmploymentStatus));
            Assert.Equal(fallbackName,
                fixtureRows.Single(row => row.EmployeeId == fixture.EligibleAdminUserId).Name);
            Assert.Equal(new[] { "EmployeeId", "EmploymentStatus", "Name" },
                typeof(GetScheduleEmployeeResponse).GetProperties().Select(property => property.Name).Order());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((2, 7, 7), fixture.CleanupCounts);
        }
    }

}

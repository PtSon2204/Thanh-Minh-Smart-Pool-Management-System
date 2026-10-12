using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;
using SmartPool.Domain.Entities;

namespace SmartPool.IntegrationTests;

public sealed class StaffScheduleIntegrationTests
{
    [Fact]
    public async Task GivenSelfScheduleScope_WhenReadingSchedule_ThenOnlyThatEmployeesAssignmentsAreReturned()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var ownShiftId = Guid.NewGuid();
        var foreignShiftId = Guid.NewGuid();

        try
        {
            await using (var setupContext = fixture.CreateContext())
            {
                setupContext.Employees.Add(new Employee
                {
                    UserId = fixture.EligibleStaffUserId,
                    BaseSalary = 10000000,
                    JoinDate = new DateOnly(2026, 10, 5),
                    Status = "Working"
                });
                setupContext.Shifts.AddRange(
                    new Shift { Id = ownShiftId, Name = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), IsActive = true },
                    new Shift { Id = foreignShiftId, Name = "Ca chiều", StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0), IsActive = true });
                setupContext.EmployeeShifts.AddRange(
                    new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = fixture.AttachedStaffUserId, ShiftId = ownShiftId, WorkDate = new DateOnly(2026, 10, 6), Status = "Scheduled" },
                    new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = fixture.EligibleStaffUserId, ShiftId = foreignShiftId, WorkDate = new DateOnly(2026, 10, 6), Status = "Scheduled" });
                await setupContext.SaveChangesAsync();
            }

            var result = await fixture.CreateOperations().GetScheduleAsync(new GetScheduleQuery
            {
                EmployeeId = fixture.AttachedStaffUserId,
                RequireEmployee = true,
                PageIndex = 1,
                PageSize = 10
            }, CancellationToken.None);

            Assert.Collection(result.Items, assignment => Assert.Equal(fixture.AttachedStaffUserId, assignment.EmployeeId));
        }
        finally
        {
            await using (var cleanupContext = fixture.CreateContext())
            {
                await cleanupContext.EmployeeShifts.Where(assignment => assignment.ShiftId == ownShiftId || assignment.ShiftId == foreignShiftId).ExecuteDeleteAsync();
                await cleanupContext.Shifts.Where(shift => shift.Id == ownShiftId || shift.Id == foreignShiftId).ExecuteDeleteAsync();
            }

            await fixture.CleanupAsync();
            Assert.Equal((2, 7, 7), fixture.CleanupCounts);
        }
    }
}

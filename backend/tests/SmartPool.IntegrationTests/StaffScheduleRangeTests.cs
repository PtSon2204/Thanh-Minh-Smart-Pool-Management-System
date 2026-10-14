using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;
using SmartPool.Domain.Entities;

namespace SmartPool.IntegrationTests;

public sealed class StaffScheduleRangeTests
{
    [Fact]
    public async Task GivenExactDate_WhenReadingSchedule_ThenOnlyAssignmentsForThatDateAreReturned()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var shiftIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var targetDate = new DateOnly(2026, 10, 6);

        try
        {
            await using (var setupContext = fixture.CreateContext())
            {
                setupContext.Shifts.AddRange(
                    new Shift { Id = shiftIds[0], Name = "Before", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), IsActive = true },
                    new Shift { Id = shiftIds[1], Name = "Target", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(13, 0), IsActive = true },
                    new Shift { Id = shiftIds[2], Name = "After", StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(14, 0), IsActive = true });
                setupContext.EmployeeShifts.AddRange(
                    new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = fixture.AttachedStaffUserId, ShiftId = shiftIds[0], WorkDate = targetDate.AddDays(-1), Status = "Scheduled" },
                    new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = fixture.AttachedStaffUserId, ShiftId = shiftIds[1], WorkDate = targetDate, Status = "Scheduled" },
                    new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = fixture.AttachedStaffUserId, ShiftId = shiftIds[2], WorkDate = targetDate.AddDays(1), Status = "Scheduled" });
                await setupContext.SaveChangesAsync(CancellationToken.None);
            }

            var result = await fixture.CreateOperations().GetScheduleAsync(new GetScheduleQuery
            {
                Date = targetDate,
                EmployeeId = fixture.AttachedStaffUserId,
                RequireEmployee = true,
                PageIndex = 1,
                PageSize = 10
            }, CancellationToken.None);

            Assert.Collection(result.Items, assignment =>
            {
                Assert.Equal(targetDate, assignment.WorkDate);
                Assert.Equal(shiftIds[1], assignment.ShiftId);
            });
        }
        finally
        {
            await using var cleanupContext = fixture.CreateContext();
            await cleanupContext.EmployeeShifts.Where(assignment => shiftIds.Contains(assignment.ShiftId)).ExecuteDeleteAsync(CancellationToken.None);
            await cleanupContext.Shifts.Where(shift => shiftIds.Contains(shift.Id)).ExecuteDeleteAsync(CancellationToken.None);
            Assert.Equal(0, await cleanupContext.EmployeeShifts.CountAsync(assignment => shiftIds.Contains(assignment.ShiftId), CancellationToken.None));

            await fixture.CleanupAsync();
            Assert.Equal((1, 7, 7), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenInclusiveRange_WhenReadingSchedule_ThenBothBoundaryDatesAreReturned()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var shiftIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var fromDate = new DateOnly(2026, 10, 6);
        var toDate = fromDate.AddDays(2);

        try
        {
            await using (var setupContext = fixture.CreateContext())
            {
                for (var index = 0; index < shiftIds.Length; index++)
                {
                    setupContext.Shifts.Add(new Shift
                    {
                        Id = shiftIds[index],
                        Name = $"Range {index}",
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(12, 0),
                        IsActive = true
                    });
                    setupContext.EmployeeShifts.Add(new EmployeeShift
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = fixture.AttachedStaffUserId,
                        ShiftId = shiftIds[index],
                        WorkDate = fromDate.AddDays(index - 1),
                        Status = "Scheduled"
                    });
                }

                await setupContext.SaveChangesAsync(CancellationToken.None);
            }

            var result = await fixture.CreateOperations().GetScheduleAsync(CreateRangeQuery(fromDate, toDate), CancellationToken.None);

            Assert.Equal(3, result.TotalCount);
            Assert.Equal([fromDate, fromDate.AddDays(1), toDate], result.Items.Select(item => item.WorkDate));
        }
        finally
        {
            await DeleteScheduleRowsAsync(fixture, shiftIds);
        }
    }

    [Theory]
    [InlineData(true, false, false, 0)]
    [InlineData(false, true, false, 0)]
    [InlineData(true, true, true, 0)]
    [InlineData(true, true, false, -1)]
    [InlineData(true, true, false, 9)]
    public void GivenInvalidRange_WhenValidatingSchedule_ThenValidationFails(bool hasFromDate, bool hasToDate, bool hasDate, int toDateOffset)
    {
        var startDate = new DateOnly(2026, 10, 6);
        var query = CreateRangeQuery(
            hasFromDate ? startDate : null,
            hasToDate ? startDate.AddDays(toDateOffset) : null,
            hasDate ? startDate : null);

        var validation = new GetScheduleValidator().Validate(query);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task GivenTiedRows_WhenPagingSchedule_ThenRowsStayUniqueAndFollowTheStableOrder()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var workDate = new DateOnly(2026, 10, 6);
        var assignments = Enumerable.Range(0, 150)
            .Select(_ => (ShiftId: Guid.NewGuid(), AssignmentId: Guid.NewGuid()))
            .ToArray();

        try
        {
            await using (var setupContext = fixture.CreateContext())
            {
                foreach (var assignment in assignments)
                {
                    setupContext.Shifts.Add(new Shift
                    {
                        Id = assignment.ShiftId,
                        Name = assignment.ShiftId.ToString(),
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(12, 0),
                        IsActive = true
                    });
                    setupContext.EmployeeShifts.Add(new EmployeeShift
                    {
                        Id = assignment.AssignmentId,
                        EmployeeId = fixture.AttachedStaffUserId,
                        ShiftId = assignment.ShiftId,
                        WorkDate = workDate,
                        Status = "Scheduled"
                    });
                }

                await setupContext.SaveChangesAsync(CancellationToken.None);
            }

            List<Guid> expectedIds;
            await using (var expectedContext = fixture.CreateContext())
            {
                expectedIds = await expectedContext.EmployeeShifts.AsNoTracking()
                    .Where(item => assignments.Select(assignment => assignment.AssignmentId).Contains(item.Id))
                    .OrderBy(item => item.WorkDate)
                    .ThenBy(item => item.Shift.StartTime)
                    .ThenBy(item => item.EmployeeId)
                    .ThenBy(item => item.ShiftId)
                    .ThenBy(item => item.Id)
                    .Select(item => item.Id)
                    .ToListAsync(CancellationToken.None);
            }

            var actualIds = new List<Guid>();
            for (var pageIndex = 1; pageIndex <= 5; pageIndex++)
            {
                var page = await fixture.CreateOperations().GetScheduleAsync(new GetScheduleQuery
                {
                    Date = workDate,
                    EmployeeId = fixture.AttachedStaffUserId,
                    RequireEmployee = true,
                    PageIndex = pageIndex,
                    PageSize = 37
                }, CancellationToken.None);
                actualIds.AddRange(page.Items.Select(item => item.Id));
            }

            Assert.Equal(expectedIds, actualIds);
            Assert.Equal(150, actualIds.Distinct().Count());
        }
        finally
        {
            await DeleteScheduleRowsAsync(fixture, assignments.Select(item => item.ShiftId).ToArray());
        }
    }

    private static GetScheduleQuery CreateRangeQuery(DateOnly? fromDate, DateOnly? toDate, DateOnly? date = null)
    {
        var query = new GetScheduleQuery
        {
            Date = date,
            PageIndex = 1,
            PageSize = 100
        };
        SetProperty(query, "FromDate", fromDate);
        SetProperty(query, "ToDate", toDate);
        return query;
    }

    private static void SetProperty(GetScheduleQuery query, string propertyName, DateOnly? value)
    {
        var property = typeof(GetScheduleQuery).GetProperty(propertyName)
            ?? throw new InvalidOperationException($"GetScheduleQuery must define {propertyName}.");
        property.SetValue(query, value);
    }

    private static async Task DeleteScheduleRowsAsync(StaffEmploymentFixture fixture, IReadOnlyCollection<Guid> shiftIds)
    {
        await using var cleanupContext = fixture.CreateContext();
        await cleanupContext.EmployeeShifts.Where(assignment => shiftIds.Contains(assignment.ShiftId)).ExecuteDeleteAsync(CancellationToken.None);
        await cleanupContext.Shifts.Where(shift => shiftIds.Contains(shift.Id)).ExecuteDeleteAsync(CancellationToken.None);
        Assert.Equal(0, await cleanupContext.EmployeeShifts.CountAsync(assignment => shiftIds.Contains(assignment.ShiftId), CancellationToken.None));

        await fixture.CleanupAsync();
        Assert.Equal((1, 7, 7), fixture.CleanupCounts);
    }
}

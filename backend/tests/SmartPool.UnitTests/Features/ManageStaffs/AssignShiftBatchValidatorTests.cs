using SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class AssignShiftBatchValidatorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenEmptyOrInvalidBatch_WhenValidating_ThenRequestIsRejected(bool empty)
    {
        var command = new AssignShiftBatchCommand
        {
            Assignments = empty
                ? []
                : [new AssignShiftBatchItem { EmployeeId = Guid.Empty, ShiftId = Guid.Empty, WorkDate = default }]
        };

        var result = await new AssignShiftBatchValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GivenBatchSpanningEightInclusiveDates_WhenValidating_ThenRequestIsRejected()
    {
        var firstDate = new DateOnly(2026, 11, 1);
        var command = new AssignShiftBatchCommand
        {
            Assignments =
            [
                new AssignShiftBatchItem { EmployeeId = Guid.NewGuid(), ShiftId = Guid.NewGuid(), WorkDate = firstDate },
                new AssignShiftBatchItem { EmployeeId = Guid.NewGuid(), ShiftId = Guid.NewGuid(), WorkDate = firstDate.AddDays(7) }
            ]
        };

        var result = await new AssignShiftBatchValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}

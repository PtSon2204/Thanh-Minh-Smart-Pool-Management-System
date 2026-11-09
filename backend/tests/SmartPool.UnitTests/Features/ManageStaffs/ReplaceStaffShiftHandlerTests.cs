using Moq;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class ReplaceStaffShiftHandlerTests
{
    [Fact]
    public async Task GivenReplacementCommand_WhenHandled_ThenOperationsReceivesTheSameCommandAndToken()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var command = new ReplaceStaffShiftCommand
        {
            Action = StaffShiftReplacementAction.Confirm,
            SourceAssignmentId = Guid.NewGuid(),
            ReplacementEmployeeId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            Reason = "Synthetic replacement"
        };
        var expected = new StaffShiftReplacementResult(command.SourceAssignmentId, Guid.NewGuid(), "Original",
            command.ReplacementEmployeeId, "Replacement", new DateOnly(2026, 11, 8), Guid.NewGuid(), "Morning",
            new TimeOnly(6, 0), new TimeOnly(12, 0), false, Guid.NewGuid(), [], "completed", null, null);
        var operations = new Mock<IStaffOperations>();
        operations.Setup(item => item.ReplaceStaffShiftAsync(command, cancellationToken)).ReturnsAsync(expected);

        var result = await new ReplaceStaffShiftHandler(operations.Object).Handle(command, cancellationToken);

        Assert.Same(expected, result);
        operations.Verify(item => item.ReplaceStaffShiftAsync(command, cancellationToken), Times.Once);
    }
}

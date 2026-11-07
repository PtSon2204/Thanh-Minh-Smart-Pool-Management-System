using Moq;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class WorkPatternHandlerTests
{
    [Fact]
    public async Task GivenCommand_WhenHandled_ThenOperationsReceivesTheSameCommandAndToken()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var command = new WorkPatternCommand { Action = WorkPatternAction.Confirm, EmployeeId = Guid.NewGuid(), ActorId = Guid.NewGuid() };
        var expected = new WorkPatternResponse(null, 0, new ScheduleResult(null, 0, 0, 0, 0, [], [], "completed"));
        var operations = new Mock<IStaffOperations>();
        operations.Setup(item => item.ExecuteWorkPatternAsync(command, cancellationToken)).ReturnsAsync(expected);

        var result = await new WorkPatternHandler(operations.Object).Handle(command, cancellationToken);

        Assert.Same(expected, result);
        operations.Verify(item => item.ExecuteWorkPatternAsync(command, cancellationToken), Times.Once);
    }
}

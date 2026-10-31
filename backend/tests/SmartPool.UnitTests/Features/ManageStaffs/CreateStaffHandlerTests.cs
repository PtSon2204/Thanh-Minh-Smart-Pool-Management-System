using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class CreateStaffHandlerTests
{
    [Fact]
    public async Task GivenEmptyUserId_WhenHandling_ThenValidationRejectsBeforeOperationAccess()
    {
        var command = ValidCommand();
        command.UserId = Guid.Empty;
        var operations = new Mock<IStaffOperations>();
        var handler = new CreateStaffHandler(operations.Object, new CreateStaffValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(CreateStaffCommand.UserId));
        Assert.All(exception.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
        operations.Verify(operation => operation.CreateStaffAsync(
            It.IsAny<CreateStaffCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenValidEmployment_WhenCreating_ThenCommandAndTokenAreForwardedAndResponseIsPreserved()
    {
        var command = ValidCommand();
        using var cancellation = new CancellationTokenSource();
        var response = new CreateStaffResponse { UserId = command.UserId, Status = "Working" };
        var operations = new Mock<IStaffOperations>();
        operations.Setup(operation => operation.CreateStaffAsync(command, cancellation.Token)).ReturnsAsync(response);
        var handler = new CreateStaffHandler(operations.Object, new CreateStaffValidator());

        var result = await handler.Handle(command, cancellation.Token);

        Assert.Same(response, result);
        operations.Verify(operation => operation.CreateStaffAsync(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenCreateOperationConflict_WhenHandling_ThenTheSameExceptionIsPropagated()
    {
        var command = ValidCommand();
        using var cancellation = new CancellationTokenSource();
        var conflict = new StaffConflictException("Employment conflict.");
        var operations = new Mock<IStaffOperations>();
        operations.Setup(operation => operation.CreateStaffAsync(command, cancellation.Token)).ThrowsAsync(conflict);
        var handler = new CreateStaffHandler(operations.Object, new CreateStaffValidator());

        var exception = await Assert.ThrowsAsync<StaffConflictException>(() => handler.Handle(command, cancellation.Token));

        Assert.Same(conflict, exception);
        operations.Verify(operation => operation.CreateStaffAsync(command, cancellation.Token), Times.Once);
    }

    private static CreateStaffCommand ValidCommand() => new()
    {
        UserId = Guid.NewGuid(), JoinDate = new DateOnly(2026, 10, 5), Status = "Working"
    };
}

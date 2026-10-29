using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class UpdateStaffHandlerTests
{
    [Fact]
    public async Task GivenInvalidEmploymentStatus_WhenHandling_ThenValidationRejectsBeforeOperationAccess()
    {
        var command = ValidCommand();
        command.Status = "Fired";
        var operations = new Mock<IStaffOperations>();
        var handler = new UpdateStaffHandler(operations.Object, new UpdateStaffValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(UpdateStaffCommand.Status));
        operations.Verify(operation => operation.UpdateStaffAsync(
            It.IsAny<UpdateStaffCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenValidEmployment_WhenUpdating_ThenCommandAndTokenAreForwardedAndResponseIsPreserved()
    {
        var command = ValidCommand();
        using var cancellation = new CancellationTokenSource();
        var response = new UpdateStaffResponse { UserId = command.UserId, Status = "Inactive" };
        var operations = new Mock<IStaffOperations>();
        operations.Setup(operation => operation.UpdateStaffAsync(command, cancellation.Token)).ReturnsAsync(response);
        var handler = new UpdateStaffHandler(operations.Object, new UpdateStaffValidator());

        var result = await handler.Handle(command, cancellation.Token);

        Assert.Same(response, result);
        operations.Verify(operation => operation.UpdateStaffAsync(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenUpdateOperationConflict_WhenHandling_ThenTheSameExceptionIsPropagated()
    {
        var command = ValidCommand();
        using var cancellation = new CancellationTokenSource();
        var conflict = new StaffConflictException("Employment conflict.");
        var operations = new Mock<IStaffOperations>();
        operations.Setup(operation => operation.UpdateStaffAsync(command, cancellation.Token)).ThrowsAsync(conflict);
        var handler = new UpdateStaffHandler(operations.Object, new UpdateStaffValidator());

        var exception = await Assert.ThrowsAsync<StaffConflictException>(() => handler.Handle(command, cancellation.Token));

        Assert.Same(conflict, exception);
        operations.Verify(operation => operation.UpdateStaffAsync(command, cancellation.Token), Times.Once);
    }

    private static UpdateStaffCommand ValidCommand() => new() { UserId = Guid.NewGuid(), Status = "Inactive" };
}

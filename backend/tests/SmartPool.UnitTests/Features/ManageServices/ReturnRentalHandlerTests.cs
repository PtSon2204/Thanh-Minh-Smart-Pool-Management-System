using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class ReturnRentalHandlerTests
{
    [Fact]
    public async Task GivenUnconfirmedSettlement_WhenHandling_ThenValidationRejectsBeforeOperationAccess()
    {
        var command = ValidCommand();
        command.SettlementConfirmed = false;
        var operations = new Mock<IServiceOperations>();
        var handler = new ReturnRentalHandler(operations.Object, new ReturnRentalValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(ReturnRentalCommand.SettlementConfirmed));
        Assert.All(exception.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
        operations.Verify(operation => operation.ReturnRentalAsync(
            It.IsAny<ReturnRentalCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenValidReturn_WhenHandling_ThenCommandAndTokenAreForwardedAndResponseIsPreserved()
    {
        var command = ValidCommand();
        using var cancellation = new CancellationTokenSource();
        var response = new ReturnRentalResponse { StockQuantity = 7 };
        var operations = new Mock<IServiceOperations>();
        operations.Setup(operation => operation.ReturnRentalAsync(command, cancellation.Token)).ReturnsAsync(response);
        var handler = new ReturnRentalHandler(operations.Object, new ReturnRentalValidator());

        var result = await handler.Handle(command, cancellation.Token);

        Assert.Same(response, result);
        operations.Verify(operation => operation.ReturnRentalAsync(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenReturnOperationConflict_WhenHandling_ThenTheSameExceptionIsPropagated()
    {
        var command = ValidCommand();
        using var cancellation = new CancellationTokenSource();
        var conflict = new ServiceConflictException("Return conflict.");
        var operations = new Mock<IServiceOperations>();
        operations.Setup(operation => operation.ReturnRentalAsync(command, cancellation.Token)).ThrowsAsync(conflict);
        var handler = new ReturnRentalHandler(operations.Object, new ReturnRentalValidator());

        var exception = await Assert.ThrowsAsync<ServiceConflictException>(() => handler.Handle(command, cancellation.Token));

        Assert.Same(conflict, exception);
        operations.Verify(operation => operation.ReturnRentalAsync(command, cancellation.Token), Times.Once);
    }

    private static ReturnRentalCommand ValidCommand() => new()
    {
        RentalId = Guid.NewGuid(), RequestId = Guid.NewGuid(), OperatorId = Guid.NewGuid(),
        Condition = "Normal", SettlementConfirmed = true
    };
}

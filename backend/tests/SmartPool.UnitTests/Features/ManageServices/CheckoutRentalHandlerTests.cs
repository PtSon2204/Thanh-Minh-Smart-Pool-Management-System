using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class CheckoutRentalHandlerTests
{
    [Fact]
    public async Task GivenUnconfirmedCollection_WhenHandling_ThenValidationRejectsBeforeOperationAccess()
    {
        var command = ValidCommand();
        command.CollectionConfirmed = false;
        var operations = new Mock<IServiceOperations>();
        var handler = new CheckoutRentalHandler(operations.Object, new CheckoutRentalValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(CheckoutRentalCommand.CollectionConfirmed));
        operations.Verify(operation => operation.CheckoutRentalAsync(
            It.IsAny<CheckoutRentalCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenValidCheckout_WhenHandling_ThenCommandAndTokenAreForwardedAndResponseIsPreserved()
    {
        var command = ValidCommand();
        using var cancellation = new CancellationTokenSource();
        var response = new CheckoutRentalResponse { OrderId = Guid.NewGuid() };
        var operations = new Mock<IServiceOperations>();
        operations.Setup(operation => operation.CheckoutRentalAsync(command, cancellation.Token)).ReturnsAsync(response);
        var handler = new CheckoutRentalHandler(operations.Object, new CheckoutRentalValidator());

        var result = await handler.Handle(command, cancellation.Token);

        Assert.Same(response, result);
        operations.Verify(operation => operation.CheckoutRentalAsync(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenCheckoutOperationConflict_WhenHandling_ThenTheSameExceptionIsPropagated()
    {
        var command = ValidCommand();
        using var cancellation = new CancellationTokenSource();
        var conflict = new ServiceConflictException("Checkout conflict.");
        var operations = new Mock<IServiceOperations>();
        operations.Setup(operation => operation.CheckoutRentalAsync(command, cancellation.Token)).ThrowsAsync(conflict);
        var handler = new CheckoutRentalHandler(operations.Object, new CheckoutRentalValidator());

        var exception = await Assert.ThrowsAsync<ServiceConflictException>(() => handler.Handle(command, cancellation.Token));

        Assert.Same(conflict, exception);
        operations.Verify(operation => operation.CheckoutRentalAsync(command, cancellation.Token), Times.Once);
    }

    private static CheckoutRentalCommand ValidCommand() => new()
    {
        RequestId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), CollectionConfirmed = true,
        CustomerName = "QA customer", CustomerPhone = "0901234567",
        Items = [new() { ProductId = Guid.NewGuid(), Quantity = 1, ExpectedUnitPrice = 10000 }]
    };
}

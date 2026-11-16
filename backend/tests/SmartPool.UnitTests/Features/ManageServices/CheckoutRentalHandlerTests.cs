using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class CheckoutRentalHandlerTests
{
    [Fact]
    public async Task GivenUnconfirmedCollection_WhenHandling_ThenValidationRejectsBeforeOperationAccess()
    {
        var command = new CheckoutRentalCommand
        {
            RequestId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), CollectionConfirmed = false,
            CustomerName = "QA customer", CustomerPhone = "0901234567",
            Items = [new() { ProductId = Guid.NewGuid(), Quantity = 1, ExpectedUnitPrice = 10000 }]
        };
        var operations = new Mock<IServiceOperations>();
        var handler = new CheckoutRentalHandler(operations.Object, new CheckoutRentalValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(CheckoutRentalCommand.CollectionConfirmed));
        Assert.All(exception.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
        operations.Verify(operation => operation.CheckoutRentalAsync(
            It.IsAny<CheckoutRentalCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}

using Moq;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.QuoteRental;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class QuoteRentalHandlerTests
{
    [Fact]
    public async Task GivenQuoteRequest_WhenHandling_ThenCommandAndTokenAreForwardedAndResponseIsPreserved()
    {
        var command = new QuoteRentalCommand { Items = [new() { ProductId = Guid.NewGuid(), Quantity = 1 }] };
        using var cancellation = new CancellationTokenSource();
        var response = new QuoteRentalResponse { TotalQuantity = 1 };
        var operations = new Mock<IServiceOperations>();
        operations.Setup(operation => operation.QuoteRentalAsync(command, cancellation.Token)).ReturnsAsync(response);
        var handler = new QuoteRentalHandler(operations.Object);

        var result = await handler.Handle(command, cancellation.Token);

        Assert.Same(response, result);
        operations.Verify(operation => operation.QuoteRentalAsync(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenQuoteOperationConflict_WhenHandling_ThenTheSameExceptionIsPropagated()
    {
        var command = new QuoteRentalCommand { Items = [new() { ProductId = Guid.NewGuid(), Quantity = 1 }] };
        using var cancellation = new CancellationTokenSource();
        var conflict = new ServiceConflictException("Quote conflict.");
        var operations = new Mock<IServiceOperations>();
        operations.Setup(operation => operation.QuoteRentalAsync(command, cancellation.Token)).ThrowsAsync(conflict);
        var handler = new QuoteRentalHandler(operations.Object);

        var exception = await Assert.ThrowsAsync<ServiceConflictException>(() => handler.Handle(command, cancellation.Token));

        Assert.Same(conflict, exception);
        operations.Verify(operation => operation.QuoteRentalAsync(command, cancellation.Token), Times.Once);
    }
}

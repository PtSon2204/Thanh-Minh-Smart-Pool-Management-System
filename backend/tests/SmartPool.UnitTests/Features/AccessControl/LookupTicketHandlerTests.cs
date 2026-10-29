using Moq;
using SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.AccessControl;

public sealed class LookupTicketHandlerTests
{
    [Fact]
    public async Task GivenWhitespaceCode_WhenHandling_ThenFailureIsReturnedWithoutOperationAccess()
    {
        var operations = new Mock<IPoolAccessOperations>();
        var handler = new LookupTicketHandler(operations.Object, new LookupTicketValidator());

        var result = await handler.Handle(new LookupTicketQuery { Code = "   " }, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.NotNull(result.Errors);
        Assert.True(result.Errors.TryGetValue(nameof(LookupTicketQuery.Code), out var errors));
        Assert.NotNull(errors);
        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.False(string.IsNullOrWhiteSpace(error)));
        operations.Verify(operation => operation.LookupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenPaddedCode_WhenHandling_ThenTrimmedCodeAndTokenAreForwardedAndResponseIsPreserved()
    {
        using var cancellation = new CancellationTokenSource();
        var response = new LookupTicketResponse();
        var operations = new Mock<IPoolAccessOperations>();
        operations.Setup(operation => operation.LookupAsync("QA-123", cancellation.Token)).ReturnsAsync(response);
        var handler = new LookupTicketHandler(operations.Object, new LookupTicketValidator());

        var result = await handler.Handle(new LookupTicketQuery { Code = "  QA-123  " }, cancellation.Token);

        Assert.True(result.IsValid);
        Assert.Null(result.Errors);
        Assert.Same(response, result.Value);
        operations.Verify(operation => operation.LookupAsync("QA-123", cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenLookupOperationFailure_WhenHandling_ThenTheSameExceptionIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        var failure = new InvalidOperationException("Lookup failed.");
        var operations = new Mock<IPoolAccessOperations>();
        operations.Setup(operation => operation.LookupAsync("QA-123", cancellation.Token)).ThrowsAsync(failure);
        var handler = new LookupTicketHandler(operations.Object, new LookupTicketValidator());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new LookupTicketQuery { Code = "QA-123" }, cancellation.Token));

        Assert.Same(failure, exception);
        operations.Verify(operation => operation.LookupAsync("QA-123", cancellation.Token), Times.Once);
    }
}

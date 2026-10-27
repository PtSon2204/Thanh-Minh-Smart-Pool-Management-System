using Moq;
using SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.AccessControl;

public sealed class ConfirmEntryHandlerTests
{
    [Fact]
    public async Task GivenInvalidInputMode_WhenHandling_ThenFailureIsReturnedWithoutOperationAccess()
    {
        var operations = new Mock<IPoolAccessOperations>();
        var handler = new ConfirmEntryHandler(operations.Object, new ConfirmEntryValidator());
        var command = new ConfirmEntryCommand { Code = "QA-123", InputMode = "Camera", OperatorId = Guid.NewGuid() };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.NotNull(result.Errors);
        Assert.True(result.Errors.TryGetValue(nameof(ConfirmEntryCommand.InputMode), out var errors));
        Assert.NotNull(errors);
        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.False(string.IsNullOrWhiteSpace(error)));
        operations.Verify(operation => operation.ConfirmAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenValidEntry_WhenHandling_ThenNormalizedArgumentsAreForwardedAndResponseIsPreserved()
    {
        var operatorId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var command = new ConfirmEntryCommand { Code = "  QA-123  ", InputMode = "Qr", OperatorId = operatorId };
        var response = new ConfirmEntryResponse();
        var operations = new Mock<IPoolAccessOperations>();
        operations.Setup(operation => operation.ConfirmAsync("QA-123", "Qr", operatorId, cancellation.Token)).ReturnsAsync(response);
        var handler = new ConfirmEntryHandler(operations.Object, new ConfirmEntryValidator());

        var result = await handler.Handle(command, cancellation.Token);

        Assert.True(result.IsValid);
        Assert.Null(result.Errors);
        Assert.Same(response, result.Value);
        operations.Verify(operation => operation.ConfirmAsync("QA-123", "Qr", operatorId, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenConfirmOperationFailure_WhenHandling_ThenTheSameExceptionIsPropagated()
    {
        var operatorId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var command = new ConfirmEntryCommand { Code = "QA-123", InputMode = "Qr", OperatorId = operatorId };
        var failure = new InvalidOperationException("Confirmation failed.");
        var operations = new Mock<IPoolAccessOperations>();
        operations.Setup(operation => operation.ConfirmAsync("QA-123", "Qr", operatorId, cancellation.Token)).ThrowsAsync(failure);
        var handler = new ConfirmEntryHandler(operations.Object, new ConfirmEntryValidator());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, cancellation.Token));

        Assert.Same(failure, exception);
        operations.Verify(operation => operation.ConfirmAsync("QA-123", "Qr", operatorId, cancellation.Token), Times.Once);
    }
}

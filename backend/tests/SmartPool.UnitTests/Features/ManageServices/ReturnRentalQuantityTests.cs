using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class ReturnRentalQuantityTests
{
    [Fact]
    public void GivenRepeatedRentalIdentity_WhenValidating_ThenItIsRejected()
    {
        var repeatedId = Guid.NewGuid();
        var result = new ReturnRentalQuantityValidator().Validate(CreateCommand([repeatedId, repeatedId]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.RentalIds));
        Assert.All(result.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
    }

    [Theory]
    [MemberData(nameof(InvalidRentalSelections))]
    public void GivenInvalidRentalSelection_WhenValidating_ThenItIsRejected(List<Guid>? ids)
    {
        var result = new ReturnRentalQuantityValidator().Validate(CreateCommand(ids));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.RentalIds));
        Assert.All(result.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
    }

    [Fact]
    public async Task GivenDuplicateRentalIdentity_WhenHandling_ThenRepositoryIsNotCalled()
    {
        var operations = new Mock<IServiceOperations>();
        var handler = new ReturnRentalQuantityHandler(operations.Object, new ReturnRentalQuantityValidator());
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            CreateCommand([id, id]), CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.RentalIds));
        Assert.All(exception.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
        operations.Verify(repository => repository.ReturnRentalQuantityAsync(
            It.IsAny<ReturnRentalQuantityCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Given101DistinctRentalIds_WhenHandling_ThenValidationRejectsBeforeRepositoryAccess()
    {
        var operations = new Mock<IServiceOperations>();
        var repositoryCalls = 0;
        operations.Setup(repository => repository.ReturnRentalQuantityAsync(
                It.IsAny<ReturnRentalQuantityCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => repositoryCalls++)
            .ReturnsAsync(new ReturnRentalQuantityResponse());
        var handler = new ReturnRentalQuantityHandler(operations.Object, new ReturnRentalQuantityValidator());
        var ids = Enumerable.Range(1, 101).Select(CreateRentalId).ToList();

        var exception = await Record.ExceptionAsync(() => handler.Handle(
            CreateCommand(ids), CancellationToken.None));

        Assert.Equal(0, repositoryCalls);
        var validation = Assert.IsType<ValidationException>(exception);
        Assert.Contains(validation.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.RentalIds));
        Assert.All(validation.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
    }

    [Fact]
    public async Task GivenValidRentalSelection_WhenHandling_ThenCommandAndTokenAreForwardedAndResponseIsPreserved()
    {
        var command = CreateCommand([Guid.NewGuid()]);
        using var cancellation = new CancellationTokenSource();
        var response = new ReturnRentalQuantityResponse();
        var operations = new Mock<IServiceOperations>();
        operations.Setup(operation => operation.ReturnRentalQuantityAsync(command, cancellation.Token)).ReturnsAsync(response);
        var handler = new ReturnRentalQuantityHandler(operations.Object, new ReturnRentalQuantityValidator());

        var result = await handler.Handle(command, cancellation.Token);

        Assert.Same(response, result);
        operations.Verify(operation => operation.ReturnRentalQuantityAsync(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenBatchReturnOperationConflict_WhenHandling_ThenTheSameExceptionIsPropagated()
    {
        var command = CreateCommand([Guid.NewGuid()]);
        using var cancellation = new CancellationTokenSource();
        var conflict = new ServiceConflictException("Batch return conflict.");
        var operations = new Mock<IServiceOperations>();
        operations.Setup(operation => operation.ReturnRentalQuantityAsync(command, cancellation.Token)).ThrowsAsync(conflict);
        var handler = new ReturnRentalQuantityHandler(operations.Object, new ReturnRentalQuantityValidator());

        var exception = await Assert.ThrowsAsync<ServiceConflictException>(() => handler.Handle(command, cancellation.Token));

        Assert.Same(conflict, exception);
        operations.Verify(operation => operation.ReturnRentalQuantityAsync(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public void Given100DistinctRentalIds_WhenValidating_ThenItIsAccepted()
    {
        var ids = Enumerable.Range(1, 100).Select(CreateRentalId).ToList();

        var result = new ReturnRentalQuantityValidator().Validate(CreateCommand(ids));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    public static TheoryData<List<Guid>?> InvalidRentalSelections => new()
    {
        null,
        new List<Guid>(),
        new List<Guid> { Guid.Empty }
    };

    private static Guid CreateRentalId(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static ReturnRentalQuantityCommand CreateCommand(List<Guid>? ids) => new()
    {
        RequestId = Guid.NewGuid(), OrderId = Guid.NewGuid(), ProductId = Guid.NewGuid(),
        OperatorId = Guid.NewGuid(), SettlementConfirmed = true, RentalIds = ids
    };
}

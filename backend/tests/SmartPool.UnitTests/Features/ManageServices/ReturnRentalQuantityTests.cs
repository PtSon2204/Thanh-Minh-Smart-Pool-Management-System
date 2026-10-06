using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class ReturnRentalQuantityTests
{
    [Fact]
    public void GivenRepeatedRentalIdentity_WhenValidating_ThenItIsRejected()
    {
        var repeatedId = Guid.NewGuid();
        var result = new ReturnRentalQuantityValidator().Validate(new ReturnRentalQuantityCommand
        {
            RentalIds = [repeatedId, repeatedId]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.RentalIds));
    }

    [Theory]
    [MemberData(nameof(InvalidRentalSelections))]
    public void GivenInvalidRentalSelection_WhenValidating_ThenItIsRejected(List<Guid>? ids)
    {
        var result = new ReturnRentalQuantityValidator().Validate(new ReturnRentalQuantityCommand { RentalIds = ids });
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GivenDuplicateRentalIdentity_WhenHandling_ThenRepositoryIsNotCalled()
    {
        var operations = new Mock<IServiceOperations>();
        var handler = new ReturnRentalQuantityHandler(operations.Object, new ReturnRentalQuantityValidator());
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new ReturnRentalQuantityCommand { RentalIds = [id, id] }, CancellationToken.None));

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
            new ReturnRentalQuantityCommand { RentalIds = ids }, CancellationToken.None));

        Assert.Equal(0, repositoryCalls);
        Assert.IsType<ValidationException>(exception);
    }

    [Fact]
    public void Given100DistinctRentalIds_WhenValidating_ThenItIsAccepted()
    {
        var ids = Enumerable.Range(1, 100).Select(CreateRentalId).ToList();

        var result = new ReturnRentalQuantityValidator().Validate(new ReturnRentalQuantityCommand { RentalIds = ids });

        Assert.True(result.IsValid);
    }

    public static TheoryData<List<Guid>?> InvalidRentalSelections => new()
    {
        null,
        new List<Guid>(),
        new List<Guid> { Guid.Empty }
    };

    private static Guid CreateRentalId(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}

using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;

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
    [InlineData(false)]
    [InlineData(true)]
    public void GivenMissingRentalSelection_WhenValidating_ThenItIsRejected(bool empty)
    {
        List<Guid>? ids = empty ? [] : null;
        var result = new ReturnRentalQuantityValidator().Validate(CreateCommand(ids));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.RentalIds));
        Assert.All(result.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
    }

    private static ReturnRentalQuantityCommand CreateCommand(List<Guid>? ids) => new()
    {
        RequestId = Guid.NewGuid(), OrderId = Guid.NewGuid(), ProductId = Guid.NewGuid(),
        OperatorId = Guid.NewGuid(), SettlementConfirmed = true, RentalIds = ids
    };
}

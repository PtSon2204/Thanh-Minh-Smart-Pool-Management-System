using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class RentalReturnValidatorTests
{
    [Fact]
    public void GivenUnconfirmedSingleRentalSettlement_WhenValidating_ThenItIsRejected()
    {
        var result = new ReturnRentalValidator().Validate(new ReturnRentalCommand
        {
            RentalId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), RequestId = Guid.NewGuid(), Condition = "Normal"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalCommand.SettlementConfirmed));
    }

    [Fact]
    public void GivenUnsupportedSingleReturnCondition_WhenValidating_ThenItIsRejected()
    {
        var result = new ReturnRentalValidator().Validate(new ReturnRentalCommand
        {
            RentalId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), RequestId = Guid.NewGuid(),
            SettlementConfirmed = true, Condition = "Partial"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalCommand.Condition));
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void GivenSingleReturnReasonAtItsMaximum_WhenValidating_ThenItAppliesTheLengthLimit(int length, bool expectedValid)
    {
        var result = new ReturnRentalValidator().Validate(new ReturnRentalCommand
        {
            RentalId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), RequestId = Guid.NewGuid(),
            SettlementConfirmed = true, Condition = "Normal", Reason = new string('R', length)
        });

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("Damaged")]
    [InlineData("Lost")]
    public void GivenNonNormalSingleReturnWithoutReason_WhenValidating_ThenItIsRejected(string condition)
    {
        var result = new ReturnRentalValidator().Validate(new ReturnRentalCommand
        {
            RentalId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), RequestId = Guid.NewGuid(),
            SettlementConfirmed = true, Condition = condition
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalCommand.Reason));
    }

    [Fact]
    public void GivenOneRentalIdInTwoConditionGroups_WhenValidating_ThenTheBatchIsRejected()
    {
        var rentalId = Guid.NewGuid();
        var command = ValidBatch([rentalId]);
        command.DamagedRentalIds = [rentalId];

        var result = new ReturnRentalQuantityValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.RentalIds));
    }

    [Fact]
    public void GivenUnconfirmedBatchSettlement_WhenValidating_ThenItIsRejected()
    {
        var command = ValidBatch([Guid.NewGuid()]);
        command.SettlementConfirmed = false;

        var result = new ReturnRentalQuantityValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.SettlementConfirmed));
    }

    [Fact]
    public void GivenDamagedBatchWithoutReason_WhenValidating_ThenItIsRejected()
    {
        var command = ValidBatch([]);
        command.DamagedRentalIds = [Guid.NewGuid()];
        command.Reason = null;

        var result = new ReturnRentalQuantityValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.Reason));
    }

    [Fact]
    public void GivenDamagedBatchReasonBeyondItsMaximum_WhenValidating_ThenItIsRejected()
    {
        var command = ValidBatch([]);
        command.DamagedRentalIds = [Guid.NewGuid()];
        command.Reason = new string('R', 501);

        var result = new ReturnRentalQuantityValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReturnRentalQuantityCommand.Reason));
    }

    private static ReturnRentalQuantityCommand ValidBatch(List<Guid> rentalIds) => new()
    {
        RequestId = Guid.NewGuid(), OrderId = Guid.NewGuid(), ProductId = Guid.NewGuid(), OperatorId = Guid.NewGuid(),
        SettlementConfirmed = true, RentalIds = rentalIds
    };
}

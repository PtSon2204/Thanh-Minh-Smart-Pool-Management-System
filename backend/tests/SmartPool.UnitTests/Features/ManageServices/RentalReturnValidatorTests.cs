using FluentValidation.Results;
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

        AssertRejected(result, nameof(ReturnRentalCommand.SettlementConfirmed));
    }

    [Fact]
    public void GivenUnsupportedSingleReturnCondition_WhenValidating_ThenItIsRejected()
    {
        var result = new ReturnRentalValidator().Validate(new ReturnRentalCommand
        {
            RentalId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), RequestId = Guid.NewGuid(),
            SettlementConfirmed = true, Condition = "Partial"
        });

        AssertRejected(result, nameof(ReturnRentalCommand.Condition));
    }

    [Fact]
    public void GivenDamagedSingleReturnWithoutReason_WhenValidating_ThenItIsRejected()
    {
        var result = new ReturnRentalValidator().Validate(new ReturnRentalCommand
        {
            RentalId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), RequestId = Guid.NewGuid(),
            SettlementConfirmed = true, Condition = "Damaged"
        });

        AssertRejected(result, nameof(ReturnRentalCommand.Reason));
    }

    [Fact]
    public void GivenOneRentalIdInTwoConditionGroups_WhenValidating_ThenTheBatchIsRejected()
    {
        var rentalId = Guid.NewGuid();
        var command = ValidBatch([rentalId]);
        command.DamagedRentalIds = [rentalId];

        var result = new ReturnRentalQuantityValidator().Validate(command);

        AssertRejected(result, nameof(ReturnRentalQuantityCommand.RentalIds));
    }

    [Fact]
    public void GivenUnconfirmedBatchSettlement_WhenValidating_ThenItIsRejected()
    {
        var command = ValidBatch([Guid.NewGuid()]);
        command.SettlementConfirmed = false;

        var result = new ReturnRentalQuantityValidator().Validate(command);

        AssertRejected(result, nameof(ReturnRentalQuantityCommand.SettlementConfirmed));
    }

    [Fact]
    public void GivenDamagedBatchWithoutReason_WhenValidating_ThenItIsRejected()
    {
        var command = ValidBatch([]);
        command.DamagedRentalIds = [Guid.NewGuid()];
        command.Reason = null;

        var result = new ReturnRentalQuantityValidator().Validate(command);

        AssertRejected(result, nameof(ReturnRentalQuantityCommand.Reason));
    }

    private static void AssertRejected(ValidationResult result, string expectedProperty)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == expectedProperty);
        Assert.All(result.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
    }

    private static ReturnRentalQuantityCommand ValidBatch(List<Guid> rentalIds) => new()
    {
        RequestId = Guid.NewGuid(), OrderId = Guid.NewGuid(), ProductId = Guid.NewGuid(), OperatorId = Guid.NewGuid(),
        SettlementConfirmed = true, RentalIds = rentalIds
    };
}

using SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry;
using SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket;

namespace SmartPool.UnitTests.Features.AccessControl;

public sealed class PoolAccessRequestValidatorTests
{
    [Fact]
    public void GivenMaximumLengthTicketCode_WhenLookingUp_ThenItIsAccepted()
    {
        var result = new LookupTicketValidator().Validate(new LookupTicketQuery { Code = new string('Q', 255) });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void GivenOverlengthTicketCode_WhenLookingUp_ThenItIsRejected()
    {
        var result = new LookupTicketValidator().Validate(new LookupTicketQuery { Code = new string('Q', 256) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LookupTicketQuery.Code));
    }

    [Fact]
    public void GivenBlankTicketCode_WhenLookingUp_ThenItIsRejected()
    {
        var result = new LookupTicketValidator().Validate(new LookupTicketQuery { Code = " " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LookupTicketQuery.Code));
    }

    [Fact]
    public void GivenInvalidMode_WhenConfirmingEntry_ThenItIsRejected()
    {
        var result = new ConfirmEntryValidator().Validate(new ConfirmEntryCommand
        {
            Code = "QA-123", InputMode = "Camera", OperatorId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ConfirmEntryCommand.InputMode));
    }

    [Fact]
    public void GivenMissingOperator_WhenConfirmingEntry_ThenItIsRejected()
    {
        var result = new ConfirmEntryValidator().Validate(new ConfirmEntryCommand
        {
            Code = "QA-123", InputMode = "Qr", OperatorId = Guid.Empty
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ConfirmEntryCommand.OperatorId));
    }
}

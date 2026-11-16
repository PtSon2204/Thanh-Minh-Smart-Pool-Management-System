using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class ReturnRentalHandlerTests
{
    [Fact]
    public async Task GivenUnconfirmedSettlement_WhenHandling_ThenValidationRejectsBeforeOperationAccess()
    {
        var command = new ReturnRentalCommand
        {
            RentalId = Guid.NewGuid(), RequestId = Guid.NewGuid(), OperatorId = Guid.NewGuid(),
            Condition = "Normal", SettlementConfirmed = false
        };
        var operations = new Mock<IServiceOperations>();
        var handler = new ReturnRentalHandler(operations.Object, new ReturnRentalValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(ReturnRentalCommand.SettlementConfirmed));
        Assert.All(exception.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
        operations.Verify(operation => operation.ReturnRentalAsync(
            It.IsAny<ReturnRentalCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}

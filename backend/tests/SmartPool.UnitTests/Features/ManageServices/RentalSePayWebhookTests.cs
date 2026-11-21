using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class RentalSePayWebhookTests
{
    [Theory]
    [InlineData("12345678")]
    [InlineData("another-account")]
    public async Task GivenIncomingRentalEvent_WhenNoApiKeyOrConfiguredAccount_ThenItUsesRentalOperations(string accountNumber)
    {
        var reference = $"RN{Guid.NewGuid():N}".ToUpperInvariant();
        var rentals = new Mock<IServiceOperations>(MockBehavior.Strict);
        rentals.Setup(item => item.ApplyTransferAsync(It.Is<BankTransferEvent>(transfer =>
                    transfer.Reference == reference && transfer.EventId == 42 && transfer.Amount == 80000 && transfer.AccountNumber == accountNumber),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BankTransferResult(true, "Recorded."));
        var controller = CreateController(rentals.Object);

        var result = await controller.RentalWebhook(Incoming(reference.ToLowerInvariant(), accountNumber: accountNumber));

        Assert.IsType<OkObjectResult>(result);
        rentals.Verify(item => item.ApplyTransferAsync(It.IsAny<BankTransferEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        rentals.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenFailedRentalCollection_WhenReceivingWebhook_ThenReturnsTicketStyleFailure()
    {
        var sender = new Mock<IServiceOperations>(MockBehavior.Strict);
        sender.Setup(item => item.ApplyTransferAsync(It.IsAny<BankTransferEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankTransferResult(false, "Not collected."));
        var controller = CreateController(sender.Object);

        var result = await controller.RentalWebhook(Incoming($"RN{Guid.NewGuid():N}"));

        var response = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        Assert.Contains("\"success\":false", json);
        sender.Verify(item => item.ApplyTransferAsync(It.IsAny<BankTransferEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenOutgoingEvent_WhenReceivingWebhook_ThenItIsIgnoredWithoutDispatch()
    {
        var sender = new Mock<IServiceOperations>(MockBehavior.Strict);
        var controller = CreateController(sender.Object);

        var outgoing = await controller.RentalWebhook(Incoming($"RN{Guid.NewGuid():N}", transferType: "out"));

        Assert.IsType<OkObjectResult>(outgoing);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenRentalEvent_WhenReceivingWebhook_ThenItPreservesReceiptFields()
    {
        var reference = $"RN{Guid.NewGuid():N}".ToUpperInvariant();
        BankTransferEvent? dispatched = null;
        var sender = new Mock<IServiceOperations>(MockBehavior.Strict);
        sender.Setup(item => item.ApplyTransferAsync(It.IsAny<BankTransferEvent>(), It.IsAny<CancellationToken>()))
            .Callback<BankTransferEvent, CancellationToken>((request, _) => dispatched = request)
            .ReturnsAsync(new BankTransferResult(true, "Recorded."));
        var controller = CreateController(sender.Object);

        var result = await controller.RentalWebhook(Incoming(reference));

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(dispatched);
        Assert.Equal(42, dispatched.EventId);
        Assert.Equal("12345678", dispatched.AccountNumber);
        Assert.Equal(80000, dispatched.Amount);
        Assert.Equal(reference, dispatched.Reference);
        sender.Verify(item => item.ApplyTransferAsync(It.IsAny<BankTransferEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenTicketReference_WhenReceivingRentalWebhook_ThenItDoesNotApplyPayment()
    {
        var rentals = new Mock<IServiceOperations>(MockBehavior.Strict);
        var controller = CreateController(rentals.Object);

        Assert.IsType<OkObjectResult>(await controller.RentalWebhook(Incoming("SPABC123")));
        rentals.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenInvalidIncomingAmount_WhenReceivingWebhook_ThenHandlerRejectsItWithoutCollecting()
    {
        var sender = new Mock<IServiceOperations>(MockBehavior.Strict);
        var controller = CreateController(sender.Object);

        var result = await controller.RentalWebhook(Incoming($"RN{Guid.NewGuid():N}", amount: 1000.5m));

        var rejection = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("\"success\":false", System.Text.Json.JsonSerializer.Serialize(rejection.Value));
        sender.VerifyNoOtherCalls();
    }

    private static RentalSePayWebhookPayload Incoming(string reference, string transferType = "in", string accountNumber = "12345678", decimal amount = 80000) => new()
    {
        id = 42, transferType = transferType, accountNumber = accountNumber, transferAmount = amount,
        content = $"Thanh toan {reference}", referenceCode = "BANK-REF-42"
    };

    private static SePayController CreateController(IServiceOperations operations)
    {
        var handler = new ConfirmRentalPaymentHandler(operations);
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(item => item.Send(It.IsAny<ConfirmRentalPaymentCommand>(), It.IsAny<CancellationToken>()))
            .Returns<ConfirmRentalPaymentCommand, CancellationToken>(handler.Handle);
        var controller = new SePayController(sender.Object);
        var context = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }
}

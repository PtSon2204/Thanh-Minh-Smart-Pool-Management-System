using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class RentalSePayWebhookTests
{
    [Fact]
    public async Task GivenInvalidWebhookKey_WhenReceivingWebhook_ThenUnauthorizedWithoutDispatch()
    {
        var sender = new Mock<IServiceOperations>(MockBehavior.Strict);
        var controller = CreateController();

        var result = await controller.RentalWebhook(Incoming($"RN{Guid.NewGuid():N}"), sender.Object, ConfiguredOptions());

        Assert.IsType<UnauthorizedObjectResult>(result);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenOutgoingOrWrongAccountEvent_WhenReceivingWebhook_ThenItIsIgnoredWithoutDispatch()
    {
        var sender = new Mock<IServiceOperations>(MockBehavior.Strict);
        var controller = CreateController(authenticated: true);

        var outgoing = await controller.RentalWebhook(Incoming($"RN{Guid.NewGuid():N}", transferType: "out"), sender.Object, ConfiguredOptions());
        var wrongAccount = await controller.RentalWebhook(Incoming($"RN{Guid.NewGuid():N}", accountNumber: "other"), sender.Object, ConfiguredOptions());

        Assert.IsType<OkObjectResult>(outgoing);
        Assert.IsType<OkObjectResult>(wrongAccount);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenAuthenticatedRentalEvent_WhenReceivingWebhook_ThenItUsesOnlyRentalOperations()
    {
        var reference = $"RN{Guid.NewGuid():N}".ToUpperInvariant();
        BankTransferEvent? dispatched = null;
        var sender = new Mock<IServiceOperations>(MockBehavior.Strict);
        sender.Setup(item => item.ApplyTransferAsync(It.IsAny<BankTransferEvent>(), It.IsAny<CancellationToken>()))
            .Callback<BankTransferEvent, CancellationToken>((request, _) => dispatched = request)
            .ReturnsAsync(new BankTransferResult(true, "Recorded."));
        var controller = CreateController(authenticated: true);

        var result = await controller.RentalWebhook(Incoming(reference), sender.Object, ConfiguredOptions());

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
        var controller = CreateController(authenticated: true);

        Assert.IsType<OkObjectResult>(await controller.RentalWebhook(Incoming("SPABC123"), rentals.Object, ConfiguredOptions()));
        rentals.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenInvalidIncomingAmount_WhenReceivingWebhook_ThenNoPaymentCommandIsSent()
    {
        var sender = new Mock<IServiceOperations>(MockBehavior.Strict);
        var controller = CreateController(authenticated: true);

        var result = await controller.RentalWebhook(Incoming($"RN{Guid.NewGuid():N}", amount: 1000.5m), sender.Object, ConfiguredOptions());

        var rejection = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, rejection.StatusCode);
        sender.VerifyNoOtherCalls();
    }

    private static RentalSePayWebhookPayload Incoming(string reference, string transferType = "in", string accountNumber = "12345678", decimal amount = 80000) => new()
    {
        id = 42, transferType = transferType, accountNumber = accountNumber, transferAmount = amount,
        content = $"Thanh toan {reference}", referenceCode = "BANK-REF-42"
    };

    private static IOptions<SePayOptions> ConfiguredOptions() => Options.Create(new SePayOptions
    {
        ApiKey = "unit-test-key", AccountNumber = "12345678", BankId = "test-bank", AccountName = "Test Counter"
    });

    private static SePayController CreateController(bool authenticated = false)
    {
        var controller = new SePayController(new Mock<ISender>(MockBehavior.Strict).Object);
        var context = new DefaultHttpContext();
        if (authenticated)
            context.Request.Headers.Authorization = "Apikey unit-test-key";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }
}

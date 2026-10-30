using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class RentalSePayWebhookTests
{
    [Fact]
    public async Task GivenMissingServerConfiguration_WhenReceivingWebhook_ThenUnavailableWithoutDispatch()
    {
        var sender = new Mock<IServiceOperations>(MockBehavior.Strict);
        var controller = CreateController();

        var result = await controller.RentalWebhook(Incoming($"RN{Guid.NewGuid():N}"), sender.Object, Options.Create(new SePayOptions()));

        var rejection = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, rejection.StatusCode);
        sender.VerifyNoOtherCalls();
    }

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
    public async Task GivenRentalReference_WhenReceivingLegacyTicketWebhook_ThenItDoesNotDispatchTicketConfirmation()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = new SePayController(sender.Object);

        Assert.IsType<OkObjectResult>(await controller.Webhook(new SePayWebhookPayload
        {
            content = $"Thanh toan RN{Guid.NewGuid():N}", transferAmount = 80000
        }));
        sender.VerifyNoOtherCalls();
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

    [Fact]
    public async Task GivenConsolidatedController_WhenPostingBothRoutes_ThenRentalRequiresAuthenticationAndTicketBehaviorIsPreserved()
    {
        var reference = $"RN{Guid.NewGuid():N}".ToUpperInvariant();
        var rentals = new Mock<IServiceOperations>(MockBehavior.Strict);
        rentals.Setup(service => service.ApplyTransferAsync(It.Is<BankTransferEvent>(transfer => transfer.Reference == reference), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankTransferResult(true, "Recorded."));
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(service => service.Send(It.Is<ConfirmPendingOrderCommand>(command => command.TransactionRef == "SPABC123"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmPendingOrderResponse { Success = true, Message = "Ticket callback preserved." });

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(SePayController).Assembly);
        builder.Services.AddSingleton(rentals.Object);
        builder.Services.AddSingleton(sender.Object);
        builder.Services.AddSingleton(ConfiguredOptions());
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            using var client = new HttpClient { BaseAddress = new Uri(addresses.Addresses.Single()), Timeout = TimeSpan.FromSeconds(10) };
            using var unauthorized = await client.PostAsJsonAsync("/api/SePay/rental/webhook", Incoming(reference));
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Apikey unit-test-key");
            using var rentalResponse = await client.PostAsJsonAsync("/api/SePay/rental/webhook", Incoming(reference));
            Assert.Equal(HttpStatusCode.OK, rentalResponse.StatusCode);
            Assert.True((await rentalResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());

            client.DefaultRequestHeaders.Clear();
            using var ticketResponse = await client.PostAsJsonAsync("/api/SePay/webhook", new SePayWebhookPayload
            {
                content = "Thanh toan SPABC123", transferAmount = 80000
            });
            Assert.Equal(HttpStatusCode.OK, ticketResponse.StatusCode);
            Assert.True((await ticketResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
            rentals.Verify(service => service.ApplyTransferAsync(It.IsAny<BankTransferEvent>(), It.IsAny<CancellationToken>()), Times.Once);
            sender.Verify(service => service.Send(It.IsAny<ConfirmPendingOrderCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            rentals.VerifyNoOtherCalls();
            sender.VerifyNoOtherCalls();
        }
        finally
        {
            await app.StopAsync();
        }
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

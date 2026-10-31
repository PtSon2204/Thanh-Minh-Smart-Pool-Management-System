using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class RentalsControllerErrorContractTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenValidationFailure_WhenCheckingOutOrReturning_Then400PreservesGroupedFieldMessages(bool checkout)
    {
        ValidationFailure[] failures =
        [
            new("RequestId", "qa-request-first"),
            new("RequestId", "qa-request-second"),
            new("OperatorId", "qa-operator")
        ];
        var exception = new ValidationException(failures);
        var sender = new Mock<ISender>();
        using var cancellation = new CancellationTokenSource();
        var controller = CreateController(sender.Object, Guid.NewGuid());
        IActionResult result;

        if (checkout)
        {
            var command = new CheckoutRentalCommand();
            sender.Setup(item => item.Send(command, cancellation.Token)).ThrowsAsync(exception);

            result = await controller.Checkout(command, cancellation.Token);

            sender.Verify(item => item.Send(command, cancellation.Token), Times.Once);
        }
        else
        {
            var command = new ReturnRentalCommand();
            sender.Setup(item => item.Send(command, cancellation.Token)).ThrowsAsync(exception);

            result = await controller.Return(Guid.NewGuid(), command, cancellation.Token);

            sender.Verify(item => item.Send(command, cancellation.Token), Times.Once);
        }

        var rejection = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, rejection.StatusCode);
        Assert.NotNull(rejection.Value);
        var body = JsonSerializer.SerializeToElement(rejection.Value);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        var errors = body.GetProperty("errors");
        Assert.Equal(new[] { "OperatorId", "RequestId" }, errors.EnumerateObject()
            .Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        foreach (var group in failures.GroupBy(failure => failure.PropertyName))
        {
            var messages = errors.GetProperty(group.Key).EnumerateArray().Select(message => message.GetString()).ToArray();
            Assert.Equal(group.Select(failure => failure.ErrorMessage), messages);
            Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GivenReturnBusinessFailure_WhenReturning_ThenStatusAndOriginalMessageArePreserved(bool conflict)
    {
        Exception failure = conflict ? new ServiceConflictException("qa-rental-conflict") : new KeyNotFoundException("qa-rental-missing");
        var command = new ReturnRentalCommand();
        var rentalId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(command, cancellation.Token)).ThrowsAsync(failure);
        var controller = CreateController(sender.Object, operatorId);

        var result = await controller.Return(rentalId, command, cancellation.Token);

        ObjectResult rejection = conflict
            ? Assert.IsType<ConflictObjectResult>(result)
            : Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(conflict ? StatusCodes.Status409Conflict : StatusCodes.Status404NotFound, rejection.StatusCode);
        Assert.NotNull(rejection.Value);
        var message = JsonSerializer.SerializeToElement(rejection.Value).GetProperty("message").GetString();
        Assert.Equal(failure.Message, message);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Equal(rentalId, command.RentalId);
        Assert.Equal(operatorId, command.OperatorId);
        sender.Verify(item => item.Send(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenUnexpectedSenderFailure_WhenReturning_ThenTheSameExceptionPropagates()
    {
        var command = new ReturnRentalCommand();
        var failure = new InvalidOperationException("qa-unexpected-rental-failure");
        using var cancellation = new CancellationTokenSource();
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(command, cancellation.Token)).ThrowsAsync(failure);
        var controller = CreateController(sender.Object, Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.Return(Guid.NewGuid(), command, cancellation.Token));

        Assert.Same(failure, exception);
        sender.Verify(item => item.Send(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenSuccessfulCheckout_WhenCheckingOut_Then201PreservesResponseAndRentalLocation()
    {
        var command = new CheckoutRentalCommand();
        var rentalId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var response = new CheckoutRentalResponse { Rentals = [new() { Id = rentalId }] };
        using var cancellation = new CancellationTokenSource();
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(command, cancellation.Token)).ReturnsAsync(response);
        var controller = CreateController(sender.Object, operatorId);

        var result = await controller.Checkout(command, cancellation.Token);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal($"/api/rentals/{rentalId}", created.Location);
        Assert.Same(response, created.Value);
        Assert.Equal(operatorId, command.OperatorId);
        sender.Verify(item => item.Send(command, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GivenSuccessfulReturn_WhenReturning_Then200PreservesResponseAndForwardsIdentity()
    {
        var command = new ReturnRentalCommand();
        var rentalId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var response = new ReturnRentalResponse { StockQuantity = 7 };
        using var cancellation = new CancellationTokenSource();
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(command, cancellation.Token)).ReturnsAsync(response);
        var controller = CreateController(sender.Object, operatorId);

        var result = await controller.Return(rentalId, command, cancellation.Token);

        var success = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, success.StatusCode);
        Assert.Same(response, success.Value);
        Assert.Equal(rentalId, command.RentalId);
        Assert.Equal(operatorId, command.OperatorId);
        sender.Verify(item => item.Send(command, cancellation.Token), Times.Once);
    }

    private static RentalsController CreateController(ISender sender, Guid userId) => new(sender)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "unit-test"))
            }
        }
    };
}

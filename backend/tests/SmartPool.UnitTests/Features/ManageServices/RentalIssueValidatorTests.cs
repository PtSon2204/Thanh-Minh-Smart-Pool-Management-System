using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.QuoteRental;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class RentalIssueValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void GivenCheckoutQuantityOutsideItsRange_WhenValidating_ThenItIsRejected(int quantity)
    {
        var command = ValidCheckout();
        command.Items[0].Quantity = quantity;

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, "Items[0].Quantity");
    }

    [Fact]
    public void GivenDuplicateCheckoutProducts_WhenValidating_ThenTheRequestIsRejected()
    {
        var command = ValidCheckout();
        command.Items.Add(new() { ProductId = command.Items[0].ProductId, Quantity = 1, ExpectedUnitPrice = 10000 });

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.Items));
    }

    [Fact]
    public void GivenCheckoutAggregateQuantityOverOneHundred_WhenValidating_ThenTheOrderIsRejected()
    {
        var command = ValidCheckout();
        command.Items[0].Quantity = 51;
        command.Items.Add(new() { ProductId = Guid.NewGuid(), Quantity = 50, ExpectedUnitPrice = 10000 });

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.Items));
    }

    [Fact]
    public void GivenCheckoutExpectedPriceIsNegative_WhenValidating_ThenItIsRejected()
    {
        var command = ValidCheckout();
        command.Items[0].ExpectedUnitPrice = -1;

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, "Items[0].ExpectedUnitPrice");
    }

    [Fact]
    public void GivenQuoteQuantityOverOneHundred_WhenValidating_ThenItIsRejected()
    {
        var command = new QuoteRentalCommand { Items = [new() { ProductId = Guid.NewGuid(), Quantity = 101 }] };

        var result = new QuoteRentalValidator().Validate(command);

        AssertRejected(result, "Items", "Items[0].Quantity");
    }

    [Fact]
    public void GivenDuplicateQuoteProducts_WhenValidating_ThenTheRequestIsRejected()
    {
        var productId = Guid.NewGuid();
        var command = new QuoteRentalCommand
        {
            Items = [new() { ProductId = productId, Quantity = 1 }, new() { ProductId = productId, Quantity = 1 }]
        };

        var result = new QuoteRentalValidator().Validate(command);

        AssertRejected(result, nameof(QuoteRentalCommand.Items));
    }

    [Fact]
    public void GivenQuoteAggregateQuantityOverOneHundred_WhenValidating_ThenTheOrderIsRejected()
    {
        var command = new QuoteRentalCommand
        {
            Items = [new() { ProductId = Guid.NewGuid(), Quantity = 51 }, new() { ProductId = Guid.NewGuid(), Quantity = 50 }]
        };

        var result = new QuoteRentalValidator().Validate(command);

        AssertRejected(result, nameof(QuoteRentalCommand.Items));
    }

    [Fact]
    public void GivenCheckoutWithoutCustomerName_WhenValidating_ThenItIsRejected()
    {
        var command = ValidCheckout();
        command.CustomerName = null;

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.CustomerName));
    }

    [Fact]
    public void GivenCheckoutPhoneWithInvalidPrefix_WhenValidating_ThenItIsRejected()
    {
        var command = ValidCheckout();
        command.CustomerPhone = "0401234567";

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.CustomerPhone));
    }

    [Fact]
    public void GivenNullCheckoutItems_WhenValidating_ThenItemsAreRejected()
    {
        var command = ValidCheckout();
        command.Items = null!;

        AssertRejected(new CheckoutRentalValidator().Validate(command), nameof(CheckoutRentalCommand.Items));
    }

    private static void AssertRejected(FluentValidation.Results.ValidationResult result, params string[] expectedProperties)
    {
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        foreach (var property in expectedProperties)
            Assert.Contains(result.Errors, error => error.PropertyName == property);
        Assert.All(result.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
    }

    private static CheckoutRentalCommand ValidCheckout() => new()
    {
        RequestId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), CollectionConfirmed = true,
        CustomerName = "QA customer", CustomerPhone = "0901234567",
        Items = [new() { ProductId = Guid.NewGuid(), Quantity = 1, ExpectedUnitPrice = 10000 }]
    };
}

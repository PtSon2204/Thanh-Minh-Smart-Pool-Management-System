using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.QuoteRental;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class RentalIssueValidatorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void GivenCheckoutQuantity_WhenValidating_ThenItAppliesTheUnitBoundary(int quantity, bool expectedValid)
    {
        var command = ValidCheckout();
        command.Items[0].Quantity = quantity;

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void GivenDuplicateCheckoutProducts_WhenValidating_ThenTheRequestIsRejected()
    {
        var command = ValidCheckout();
        command.Items.Add(new() { ProductId = command.Items[0].ProductId, Quantity = 1, ExpectedUnitPrice = 10000 });

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRentalCommand.Items));
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void GivenCheckoutLineCountAtItsBoundary_WhenValidating_ThenItAppliesTheLineLimit(int lineCount, bool expectedValid)
    {
        var command = ValidCheckout();
        command.Items = Enumerable.Range(1, lineCount)
            .Select(_ => new CheckoutRentalLine { ProductId = Guid.NewGuid(), Quantity = 1, ExpectedUnitPrice = 10000 })
            .ToList();

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(50, 50, true)]
    [InlineData(51, 50, false)]
    public void GivenCheckoutAggregateQuantityAtItsBoundary_WhenValidating_ThenItAppliesTheOrderLimit(
        int firstQuantity, int secondQuantity, bool expectedValid)
    {
        var command = ValidCheckout();
        command.Items[0].Quantity = firstQuantity;
        command.Items.Add(new() { ProductId = Guid.NewGuid(), Quantity = secondQuantity, ExpectedUnitPrice = 10000 });

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void GivenCheckoutExpectedPriceAtItsLowerBoundary_WhenValidating_ThenItAppliesThePriceRule(decimal price, bool expectedValid)
    {
        var command = ValidCheckout();
        command.Items[0].ExpectedUnitPrice = price;

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void GivenCheckoutNameBeyondItsMaximum_WhenValidating_ThenTheNameIsRejected()
    {
        var command = ValidCheckout();
        command.CustomerName = new string('N', 256);

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRentalCommand.CustomerName));
    }

    [Fact]
    public void GivenCheckoutPhoneBeyondItsMaximum_WhenValidating_ThenThePhoneIsRejected()
    {
        var command = ValidCheckout();
        command.CustomerPhone = new string('1', 21);

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRentalCommand.CustomerPhone));
    }

    [Fact]
    public void GivenCheckoutWithoutCollectionConfirmation_WhenValidating_ThenItIsRejected()
    {
        var command = ValidCheckout();
        command.CollectionConfirmed = false;

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRentalCommand.CollectionConfirmed));
    }

    [Fact]
    public void GivenCheckoutLineWithoutProductId_WhenValidating_ThenItIsRejected()
    {
        var command = ValidCheckout();
        command.Items[0].ProductId = Guid.Empty;

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Items[0].ProductId");
    }

    [Fact]
    public void GivenCheckoutWithoutRequestOrOperatorId_WhenValidating_ThenBothIdsAreRejected()
    {
        var command = ValidCheckout();
        command.RequestId = Guid.Empty;
        command.OperatorId = Guid.Empty;

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRentalCommand.RequestId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRentalCommand.OperatorId));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void GivenQuoteQuantity_WhenValidating_ThenItAppliesTheOrderBoundary(int quantity, bool expectedValid)
    {
        var command = new QuoteRentalCommand { Items = [new() { ProductId = Guid.NewGuid(), Quantity = quantity }] };

        var result = new QuoteRentalValidator().Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
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

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(QuoteRentalCommand.Items));
    }

    [Fact]
    public void GivenQuoteAggregateQuantityOverOneHundred_WhenValidating_ThenTheOrderIsRejected()
    {
        var command = new QuoteRentalCommand
        {
            Items = [new() { ProductId = Guid.NewGuid(), Quantity = 51 }, new() { ProductId = Guid.NewGuid(), Quantity = 50 }]
        };

        var result = new QuoteRentalValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(QuoteRentalCommand.Items));
    }

    [Fact]
    public void GivenQuoteLineWithoutProductId_WhenValidating_ThenItIsRejected()
    {
        var result = new QuoteRentalValidator().Validate(new QuoteRentalCommand
        {
            Items = [new() { ProductId = Guid.Empty, Quantity = 1 }]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Items[0].ProductId");
    }

    private static CheckoutRentalCommand ValidCheckout() => new()
    {
        RequestId = Guid.NewGuid(), OperatorId = Guid.NewGuid(), CollectionConfirmed = true,
        CustomerName = "QA customer", CustomerPhone = "0901234567",
        Items = [new() { ProductId = Guid.NewGuid(), Quantity = 1, ExpectedUnitPrice = 10000 }]
    };
}

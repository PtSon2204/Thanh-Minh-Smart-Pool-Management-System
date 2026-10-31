using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation.Results;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.QuoteRental;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class RentalIssueValidatorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void GivenCheckoutQuantity_WhenValidating_ThenItAppliesTheUnitBoundary(int quantity, bool expectedValid)
    {
        var command = ValidCheckout();
        command.Items[0].Quantity = quantity;

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
        if (expectedValid)
            Assert.Empty(result.Errors);
        else if (quantity == 101)
            AssertRejected(result, "Items", "Items[0].Quantity");
        else
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
        if (expectedValid)
            Assert.Empty(result.Errors);
        else
            AssertRejected(result, nameof(CheckoutRentalCommand.Items));
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
        if (expectedValid)
            Assert.Empty(result.Errors);
        else
            AssertRejected(result, nameof(CheckoutRentalCommand.Items));
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
        if (expectedValid)
            Assert.Empty(result.Errors);
        else
            AssertRejected(result, "Items[0].ExpectedUnitPrice");
    }

    [Fact]
    public void GivenCheckoutNameBeyondItsMaximum_WhenValidating_ThenTheNameIsRejected()
    {
        var command = ValidCheckout();
        command.CustomerName = new string('N', 256);

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.CustomerName));
    }

    [Fact]
    public void GivenCheckoutPhoneBeyondItsMaximum_WhenValidating_ThenThePhoneIsRejected()
    {
        var command = ValidCheckout();
        command.CustomerPhone = new string('1', 21);

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.CustomerPhone));
    }

    [Fact]
    public void GivenCheckoutWithoutCollectionConfirmation_WhenValidating_ThenItIsRejected()
    {
        var command = ValidCheckout();
        command.CollectionConfirmed = false;

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.CollectionConfirmed));
    }

    [Fact]
    public void GivenCheckoutLineWithoutProductId_WhenValidating_ThenItIsRejected()
    {
        var command = ValidCheckout();
        command.Items[0].ProductId = Guid.Empty;

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, "Items[0].ProductId");
    }

    [Fact]
    public void GivenCheckoutWithoutRequestOrOperatorId_WhenValidating_ThenBothIdsAreRejected()
    {
        var command = ValidCheckout();
        command.RequestId = Guid.Empty;
        command.OperatorId = Guid.Empty;

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.RequestId), nameof(CheckoutRentalCommand.OperatorId));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void GivenQuoteQuantity_WhenValidating_ThenItAppliesTheOrderBoundary(int quantity, bool expectedValid)
    {
        var command = new QuoteRentalCommand { Items = [new() { ProductId = Guid.NewGuid(), Quantity = quantity }] };

        var result = new QuoteRentalValidator().Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
        if (expectedValid)
            Assert.Empty(result.Errors);
        else
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
    public void GivenQuoteLineWithoutProductId_WhenValidating_ThenItIsRejected()
    {
        var result = new QuoteRentalValidator().Validate(new QuoteRentalCommand
        {
            Items = [new() { ProductId = Guid.Empty, Quantity = 1 }]
        });

        AssertRejected(result, "Items[0].ProductId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenMissingCheckoutName_WhenValidating_ThenTheNameIsRejected(string? name)
    {
        var command = ValidCheckout();
        command.CustomerName = name;

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.CustomerName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenMissingCheckoutPhone_WhenValidating_ThenThePhoneIsRejected(string? phone)
    {
        var command = ValidCheckout();
        command.CustomerPhone = phone;

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.CustomerPhone));
    }

    [Fact]
    public void GivenCheckoutNameAtItsMaximum_WhenValidating_ThenItIsAccepted()
    {
        var command = ValidCheckout();
        command.CustomerName = new string('N', 255);

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void GivenCheckoutPhoneAtItsMaximum_WhenValidating_ThenItIsAccepted()
    {
        var command = ValidCheckout();
        command.CustomerPhone = new string('1', 20);

        var result = new CheckoutRentalValidator().Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    public void GivenInvalidCheckoutItemsJson_WhenValidating_ThenItemsAreRejected(string itemsJson)
    {
        var json = JsonSerializer.SerializeToNode(ValidCheckout());
        Assert.NotNull(json);
        json[nameof(CheckoutRentalCommand.Items)] = JsonNode.Parse(itemsJson);
        var command = json.Deserialize<CheckoutRentalCommand>();
        Assert.NotNull(command);
        // OperatorId comes from server context, not the JSON payload.
        command.OperatorId = Guid.NewGuid();

        var result = new CheckoutRentalValidator().Validate(command);

        AssertRejected(result, nameof(CheckoutRentalCommand.Items));
        Assert.All(result.Errors, error => Assert.Equal(nameof(CheckoutRentalCommand.Items), error.PropertyName));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    public void GivenInvalidQuoteItemsJson_WhenValidating_ThenItemsAreRejected(string itemsJson)
    {
        var command = JsonSerializer.Deserialize<QuoteRentalCommand>($$"""{"Items":{{itemsJson}}}""");
        Assert.NotNull(command);

        var result = new QuoteRentalValidator().Validate(command);

        AssertRejected(result, nameof(QuoteRentalCommand.Items));
        Assert.All(result.Errors, error => Assert.Equal(nameof(QuoteRentalCommand.Items), error.PropertyName));
    }

    [Fact]
    public void GivenQuoteQuantityBelowItsMinimum_WhenValidating_ThenTheQuantityIsRejected()
    {
        var command = new QuoteRentalCommand { Items = [new() { ProductId = Guid.NewGuid(), Quantity = 0 }] };

        var result = new QuoteRentalValidator().Validate(command);

        AssertRejected(result, "Items[0].Quantity");
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void GivenQuoteLineCountAtItsBoundary_WhenValidating_ThenItAppliesTheLineLimit(int lineCount, bool expectedValid)
    {
        var command = new QuoteRentalCommand
        {
            Items = Enumerable.Range(1, lineCount)
                .Select(_ => new QuoteRentalLine { ProductId = Guid.NewGuid(), Quantity = 1 })
                .ToList()
        };

        var result = new QuoteRentalValidator().Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
        if (expectedValid)
            Assert.Empty(result.Errors);
        else
            AssertRejected(result, nameof(QuoteRentalCommand.Items));
    }

    [Fact]
    public void GivenQuoteAggregateQuantityAtOneHundred_WhenValidating_ThenItIsAccepted()
    {
        var command = new QuoteRentalCommand
        {
            Items = [new() { ProductId = Guid.NewGuid(), Quantity = 50 }, new() { ProductId = Guid.NewGuid(), Quantity = 50 }]
        };

        var result = new QuoteRentalValidator().Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static void AssertRejected(ValidationResult result, params string[] expectedProperties)
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

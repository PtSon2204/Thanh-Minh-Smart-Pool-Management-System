using SmartPool.Application.Features.GoodsReceipts;
using SmartPool.Application.Features.ManageServices.Commands.CreateService;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class GoodsReceiptValidatorTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(10, false)]
    public void NewProductMustUseReceiptToReceiveStock(int? quantity, bool expected)
    {
        var result = new CreateServiceValidator().Validate(new CreateServiceCommand
        { Name = "Phao", Type = "Sale", Price = 100000, StockQuantity = quantity });
        Assert.Equal(expected, result.IsValid);
    }
    [Fact]
    public void ReceiptRequiresDetailsAndUniqueProducts()
    {
        var command = ValidCommand();
        command.Details.Clear();
        Assert.False(new CreateGoodsReceiptValidator().Validate(command).IsValid);
        var productId = Guid.NewGuid();
        command.Details = [new() { ProductId = productId, Quantity = 1, UnitCost = 50000 },
            new() { ProductId = productId, Quantity = 2, UnitCost = 60000 }];
        Assert.False(new CreateGoodsReceiptValidator().Validate(command).IsValid);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, -1)]
    [InlineData(1, 0.001)]
    public void InvalidQuantityOrCostIsRejected(int quantity, decimal unitCost)
    {
        var command = ValidCommand();
        command.Details[0].Quantity = quantity;
        command.Details[0].UnitCost = unitCost;
        Assert.False(new CreateGoodsReceiptValidator().Validate(command).IsValid);
    }

    [Fact]
    public void ValidReceiptIncludingZeroCostIsAccepted()
    {
        var command = ValidCommand();
        command.Details[0].UnitCost = 0;
        Assert.True(new CreateGoodsReceiptValidator().Validate(command).IsValid);
    }

    [Fact]
    public void NullLineAndTimestampWithoutTimezoneAreRejected()
    {
        var command = ValidCommand();
        command.Details = [null!];
        Assert.False(new CreateGoodsReceiptValidator().Validate(command).IsValid);
        command = ValidCommand();
        command.ReceivedAt = DateTime.SpecifyKind(command.ReceivedAt, DateTimeKind.Unspecified);
        Assert.False(new CreateGoodsReceiptValidator().Validate(command).IsValid);
    }

    private static CreateGoodsReceiptCommand ValidCommand() => new()
    {
        Id = Guid.NewGuid(), ReceiptCode = "PN001", OperatorId = Guid.NewGuid(), ReceivedAt = DateTime.UtcNow,
        Details = [new() { ProductId = Guid.NewGuid(), Quantity = 1, UnitCost = 50000 }]
    };
}

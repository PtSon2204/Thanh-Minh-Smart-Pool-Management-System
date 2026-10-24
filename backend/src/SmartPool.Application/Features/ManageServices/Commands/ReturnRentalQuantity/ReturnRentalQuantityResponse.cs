namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;

public sealed class ReturnRentalQuantityResponse
{
    public Guid OrderId { get; init; }
    public Guid ProductId { get; init; }
    public int ReturnedQuantity { get; init; }
    public bool AlreadyReturned { get; init; }
    public int StockQuantity { get; init; }
    public int RentedQuantity { get; init; }
    public int TotalReturnedQuantity { get; init; }
    public int OutstandingQuantity { get; init; }
    public Guid RequestId { get; init; }
    public int NormalQuantity { get; init; }
    public int DamagedQuantity { get; init; }
    public int LostQuantity { get; init; }
    public decimal RefundAmount { get; init; }
    public decimal RetainedAmount { get; init; }
    public decimal DepositAmount { get; init; }
    public string OrderStatus { get; init; } = string.Empty;
}

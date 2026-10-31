namespace SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

public sealed class GetRentalDetailsResponse
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? OrderStatus { get; set; }
    public DateTime? RentTime { get; set; }
    public DateTime? ReturnTime { get; set; }
    public int RentedQuantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public int OutstandingQuantity { get; set; }
    public decimal TotalDepositAmount { get; set; }
    public bool CollectionVerified { get; set; }
    public decimal RentalFeeTotal { get; set; }
    public decimal RefundedDepositAmount { get; set; }
    public decimal RetainedDepositAmount { get; set; }
    public decimal OutstandingDepositAmount { get; set; }
    public decimal OrderCollectedAmount { get; set; }
    public int DamagedQuantity { get; set; }
    public int LostQuantity { get; set; }
    public int ResolvedQuantity { get; set; }
    public List<RentalTransactionDetails> Transactions { get; set; } = [];
    public List<Guid> OutstandingRentalIds { get; set; } = [];
    public List<RentalRecordDetails> Rentals { get; set; } = [];
}

public sealed class RentalTransactionDetails
{
    public string? CollectionMethod { get; set; }
    public string? RefundMethod { get; set; }
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal RetainedAmount { get; set; }
    public Guid ConfirmedByUserId { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
    public string? Reason { get; set; }
}

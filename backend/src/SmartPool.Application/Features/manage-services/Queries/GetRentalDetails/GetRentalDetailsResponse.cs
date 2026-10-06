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
    public List<Guid> OutstandingRentalIds { get; set; } = [];
    public List<RentalRecordDetails> Rentals { get; set; } = [];
}

public sealed class RentalRecordDetails
{
    public Guid Id { get; set; }
    public DateTime? RentTime { get; set; }
    public DateTime? ReturnTime { get; set; }
    public decimal? DepositAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

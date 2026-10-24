namespace SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

public sealed class RentalRecordDetails
{
    public Guid Id { get; set; }
    public DateTime? RentTime { get; set; }
    public DateTime? ReturnTime { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? FeeAmount { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? SettlementId { get; set; }
    public string Status { get; set; } = string.Empty;
}

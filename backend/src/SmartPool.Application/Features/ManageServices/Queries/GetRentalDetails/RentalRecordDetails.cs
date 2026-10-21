namespace SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

public sealed class RentalRecordDetails
{
    public Guid Id { get; set; }
    public DateTime? RentTime { get; set; }
    public DateTime? ReturnTime { get; set; }
    public decimal? DepositAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

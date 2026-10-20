namespace SmartPool.Application.Features.ManageStaffs.Queries.GetAttendanceHistory;

public sealed class GetAttendanceHistoryResponse
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public Guid PerformedByUserId { get; set; }
    public string PerformedByUsername { get; set; } = string.Empty;
    public DateTime PerformedAtUtc { get; set; }
    public string? Reason { get; set; }
    public DateTime? BeforeCheckInTime { get; set; }
    public DateTime? BeforeCheckOutTime { get; set; }
    public string? BeforeCheckInMethod { get; set; }
    public string? BeforeCheckOutMethod { get; set; }
    public string? BeforeStatus { get; set; }
    public DateTime? AfterCheckInTime { get; set; }
    public DateTime? AfterCheckOutTime { get; set; }
    public string? AfterCheckInMethod { get; set; }
    public string? AfterCheckOutMethod { get; set; }
    public string? AfterStatus { get; set; }
}

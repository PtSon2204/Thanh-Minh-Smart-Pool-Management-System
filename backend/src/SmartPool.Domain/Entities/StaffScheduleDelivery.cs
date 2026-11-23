namespace SmartPool.Domain.Entities;

public sealed class StaffScheduleDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OperationId { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid RecipientId { get; set; }
    public string Channel { get; set; } = "EMAIL";
    public string RecipientEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string HtmlBody { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public string? LastErrorCode { get; set; }
}

namespace SmartPool.Domain.Entities;

public sealed class StaffScheduleChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string Kind { get; set; } = "";
    public long? AppliedVersion { get; set; }
    public string? ContractJson { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? WorkDate { get; set; }
    public string? SourceAssignmentIdsJson { get; set; }
    public Guid? DestinationShiftId { get; set; }
    public Guid? DestinationEmployeeId { get; set; }
    public string? Reason { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? OperationId { get; set; }
    public string? ProposalHash { get; set; }
    public string? ResponseJson { get; set; }
}

namespace SmartPool.Domain.Entities;

public sealed class StaffWorkPattern
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string ContractJson { get; set; } = "";
    public DateOnly EffectiveFrom { get; set; }
    public long Version { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class StaffScheduleException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string Kind { get; set; } = "";
    public DateOnly WorkDate { get; set; }
    public string SourceAssignmentIdsJson { get; set; } = "[]";
    public Guid? DestinationShiftId { get; set; }
    public Guid? DestinationEmployeeId { get; set; }
    public string Reason { get; set; } = "";
    public long Version { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class StaffSchedulePreview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid ActorId { get; set; }
    public string ProposalHash { get; set; } = "";
    public string StateHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class StaffScheduleOperation
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid ActorId { get; set; }
    public string ProposalHash { get; set; } = "";
    public string ResponseJson { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class StaffScheduleDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OperationId { get; set; }
    public Guid EmployeeId { get; set; }
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

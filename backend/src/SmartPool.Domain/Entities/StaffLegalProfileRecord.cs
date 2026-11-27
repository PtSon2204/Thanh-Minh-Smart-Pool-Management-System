namespace SmartPool.Domain.Entities;

// One encrypted legal profile per employee, keyed by the employee's user id.
// Replaces the per-revision storage used by the retired onboarding flow.
public sealed class StaffLegalProfileRecord
{
    public Guid EmployeeId { get; set; }

    // AES-GCM encrypted StaffLegalProfile JSON, protected by StaffOnboardingServices.
    public string ProtectedProfile { get; set; } = "";

    public string? CccdDigest { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public long Version { get; set; } = 1;
}

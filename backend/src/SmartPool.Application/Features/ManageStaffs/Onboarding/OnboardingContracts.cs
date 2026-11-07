using System.Text.Json.Serialization;
using MediatR;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;

namespace SmartPool.Application.Features.ManageStaffs.Onboarding;

public sealed record StaffLegalProfile
{
    public string FullName { get; init; } = "";
    public string Phone { get; init; } = "";
    public DateOnly? DateOfBirth { get; init; }
    public string CccdNumber { get; init; } = "";
    public string PermanentAddress { get; init; } = "";
    public string TemporaryAddress { get; init; } = "";
    public string BankName { get; init; } = "";
    public string BankAccountNumber { get; init; } = "";
    public string TaxIdentifierKind { get; init; } = "PERSONAL_ID";
    public string TaxIdentifier { get; init; } = "";
    public string EmergencyContactName { get; init; } = "";
    public string EmergencyContactPhone { get; init; } = "";
    public string EmergencyContactRelationship { get; init; } = "";
}

public enum OnboardingAction
{
    Create, Inspect, Detail, Resend, Cancel, Draft, Submit, Review, Approve,
    RequestChanges, Reject, Own, RequestCorrection, OpenCorrection, Upload, EmployeeDetail, Contract
}

public sealed class OnboardingCommand : IRequest<OnboardingWorkspace>
{
    [JsonIgnore] public OnboardingAction Action { get; set; }
    [JsonIgnore] public Guid ActorId { get; set; }
    [JsonIgnore] public Guid? EmployeeId { get; set; }
    [JsonIgnore] public Guid? RevisionId { get; set; }
    [JsonIgnore] public StaffDocumentUpload? Upload { get; set; }
    public Guid? InvitationId { get; set; }
    public string? Token { get; set; }
    public long? ExpectedVersion { get; set; }
    public string? Email { get; set; }
    public string? TargetRole { get; set; }
    public DateOnly? JoinDate { get; set; }
    public ContractTerms? Contract { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public StaffLegalProfile? Profile { get; set; }
    public string? Password { get; set; }
    public string? PasswordConfirmation { get; set; }
    public string? Reason { get; set; }
    public bool ReviewConfirmed { get; set; }
}

public sealed record StaffDocumentUpload(string Purpose, string ContentType, byte[] Bytes);
public sealed record StaffDocumentView(Guid Id, string Purpose, string Status, string ContentType,
    DateTime UploadedAtUtc, DateTime? DeleteAfterUtc, DateTime? DeletedAtUtc, string? DeletionErrorCode);
public sealed record StaffRevisionView(Guid Id, int RevisionNumber, string State, long Version,
    StaffLegalProfile Profile, IReadOnlyList<StaffDocumentView> Documents, DateTime? SubmittedAtUtc,
    DateTime? ReviewedAtUtc, Guid? ReviewerId, string? CorrectionReason, bool DocumentsProvided,
    string? ReviewerName = null);
public sealed record StaffInvitationView(Guid Id, string Email, string TargetRole, DateOnly JoinDate,
    string Status, string DeliveryStatus, string? DeliveryErrorCode, DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc, Guid? CompletedUserId, long Version, ContractTerms? Contract = null,
    IReadOnlyList<ContractShiftView>? Shifts = null);
public sealed record StaffCorrectionView(Guid Id, Guid EmployeeId, string Reason, string State,
    DateTime CreatedAtUtc, Guid? RevisionId, long Version);
public sealed record OnboardingWorkspace(string Code, StaffInvitationView? Invitation,
    StaffRevisionView? Revision, StaffRevisionView? ApprovedRevision, StaffCorrectionView? CorrectionRequest,
    ScheduleResult? ScheduleResult = null, ContractTerms? Contract = null,
    IReadOnlyList<ContractShiftView>? Shifts = null);
public sealed record StaffInvitationListItem(StaffInvitationView Invitation, Guid? RevisionId,
    string? FullName, string? ReviewStatus, DateTime? SubmittedAtUtc);
public sealed record ListStaffInvitationsQuery(int Page = 1, int PageSize = 30)
    : IRequest<IReadOnlyList<StaffInvitationListItem>>
{
    [JsonIgnore] public Guid ActorId { get; init; }
}
public sealed record StaffDocumentContent(byte[] Bytes, string ContentType);
public sealed record ReadStaffDocumentQuery(Guid DocumentId, Guid ActorId, bool IsAdmin,
    Guid? InvitationId, string? Token) : IRequest<StaffDocumentContent>;

public sealed class StaffOnboardingException(string code, int status = 409) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

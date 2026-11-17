namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffEdit;

public sealed record GetStaffEditResponse(
    Guid UserId, string? Status, StaffEditValues Details,
    DateTime? ExpectedUpdatedAt, DateTime? ExpectedUserUpdatedAt, DateTime? ExpectedProfileUpdatedAt,
    bool ExpectedProfileExists, Guid? ExpectedApprovedRevisionId, long? ExpectedApprovedRevisionVersion,
    long ExpectedCorrectionVersion, bool DetailsEditingBlocked);

public sealed record StaffEditValues(
    string? FullName, string? Phone, string? Address, DateOnly? DateOfBirth,
    string? CccdNumber, string? PermanentAddress, string? TemporaryAddress,
    string? BankName, string? BankAccountNumber, string? TaxIdentifierKind, string? TaxIdentifier);

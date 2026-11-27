namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffEdit;

public sealed record GetStaffEditResponse(
    Guid UserId, string? Status, StaffEditValues Details,
    DateTime? ExpectedUpdatedAt, DateTime? ExpectedUserUpdatedAt, DateTime? ExpectedProfileUpdatedAt,
    bool ExpectedProfileExists, long? ExpectedLegalProfileVersion);

public sealed record StaffEditValues(
    string? FullName, string? Phone, string? Address, DateOnly? DateOfBirth,
    string? CccdNumber, string? PermanentAddress, string? TemporaryAddress,
    string? BankName, string? BankAccountNumber, string? TaxIdentifierKind, string? TaxIdentifier,
    string? Gender, string? PersonalEmail, string? EmergencyContactName, string? EmergencyContactPhone,
    string? EmergencyContactRelationship);

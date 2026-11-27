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

public sealed class StaffOnboardingException(string code, int status = 409) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

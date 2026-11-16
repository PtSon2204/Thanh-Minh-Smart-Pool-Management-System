using System.Text.Json.Serialization;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StaffEditDetails
{
    private readonly HashSet<string> supplied = [];
    private string? fullName, phone, address, cccdNumber, permanentAddress, temporaryAddress;
    private string? bankName, bankAccountNumber, taxIdentifierKind, taxIdentifier;
    private DateOnly? dateOfBirth;

    public string? FullName { get => fullName; set { fullName = value; supplied.Add(nameof(FullName)); } }
    public string? Phone { get => phone; set { phone = value; supplied.Add(nameof(Phone)); } }
    public string? Address { get => address; set { address = value; supplied.Add(nameof(Address)); } }
    public DateOnly? DateOfBirth { get => dateOfBirth; set { dateOfBirth = value; supplied.Add(nameof(DateOfBirth)); } }
    public string? CccdNumber { get => cccdNumber; set { cccdNumber = value; supplied.Add(nameof(CccdNumber)); } }
    public string? PermanentAddress { get => permanentAddress; set { permanentAddress = value; supplied.Add(nameof(PermanentAddress)); } }
    public string? TemporaryAddress { get => temporaryAddress; set { temporaryAddress = value; supplied.Add(nameof(TemporaryAddress)); } }
    public string? BankName { get => bankName; set { bankName = value; supplied.Add(nameof(BankName)); } }
    public string? BankAccountNumber { get => bankAccountNumber; set { bankAccountNumber = value; supplied.Add(nameof(BankAccountNumber)); } }
    public string? TaxIdentifierKind { get => taxIdentifierKind; set { taxIdentifierKind = value; supplied.Add(nameof(TaxIdentifierKind)); } }
    public string? TaxIdentifier { get => taxIdentifier; set { taxIdentifier = value; supplied.Add(nameof(TaxIdentifier)); } }

    public bool Has(string property) => supplied.Contains(property);

    public StaffLegalProfile Merge(StaffLegalProfile current)
    {
        var merged = current with
        {
            FullName = Text(nameof(FullName), FullName, current.FullName),
            Phone = Has(nameof(Phone)) ? StaffOnboardingValidation.Phone(Text(nameof(Phone), Phone, current.Phone)) : current.Phone,
            DateOfBirth = Has(nameof(DateOfBirth))
                ? DateOfBirth ?? throw new StaffOnboardingException("REQUIRED_FIELD_CANNOT_BE_CLEARED", 400) : current.DateOfBirth,
            CccdNumber = Text(nameof(CccdNumber), CccdNumber, current.CccdNumber),
            PermanentAddress = Text(nameof(PermanentAddress), PermanentAddress, current.PermanentAddress),
            TemporaryAddress = Text(nameof(TemporaryAddress), TemporaryAddress, current.TemporaryAddress),
            BankName = Text(nameof(BankName), BankName, current.BankName),
            BankAccountNumber = Text(nameof(BankAccountNumber), BankAccountNumber, current.BankAccountNumber),
            TaxIdentifierKind = Text(nameof(TaxIdentifierKind), TaxIdentifierKind, current.TaxIdentifierKind),
            TaxIdentifier = Text(nameof(TaxIdentifier), TaxIdentifier, current.TaxIdentifier)
        };
        if (merged.TaxIdentifierKind == "PERSONAL_ID" && merged.CccdNumber.Length > 0 && !Has(nameof(TaxIdentifier)) &&
            (Has(nameof(CccdNumber)) || Has(nameof(TaxIdentifierKind))))
            merged = merged with { TaxIdentifier = merged.CccdNumber };
        return merged;
    }

    private string Text(string property, string? value, string current)
    {
        if (!Has(property)) return current;
        if (string.IsNullOrWhiteSpace(value)) throw new StaffOnboardingException("REQUIRED_FIELD_CANNOT_BE_CLEARED", 400);
        return value.Trim();
    }
}

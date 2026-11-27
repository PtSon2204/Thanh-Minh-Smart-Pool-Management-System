using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StaffEditDetails
{
    private readonly HashSet<string> supplied = [];
    private string? fullName, phone, address, cccdNumber, permanentAddress, temporaryAddress;
    private string? bankName, bankAccountNumber, taxIdentifierKind, taxIdentifier, gender, personalEmail;
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
    public string? Gender { get => gender; set { gender = value; supplied.Add(nameof(Gender)); } }
    public string? PersonalEmail { get => personalEmail; set { personalEmail = value; supplied.Add(nameof(PersonalEmail)); } }

    public bool Has(string property) => supplied.Contains(property);

    public StaffLegalProfile Merge(StaffLegalProfile current)
    {
        var merged = current with
        {
            FullName = Text(nameof(FullName), FullName, current.FullName),
            Phone = Has(nameof(Phone)) ? StaffOnboardingValidation.Phone(Text(nameof(Phone), Phone, current.Phone)) : current.Phone,
            DateOfBirth = Has(nameof(DateOfBirth))
                ? DateOfBirth ?? throw Required(nameof(DateOfBirth)) : current.DateOfBirth,
            CccdNumber = Text(nameof(CccdNumber), CccdNumber, current.CccdNumber),
            PermanentAddress = Text(nameof(PermanentAddress), PermanentAddress, current.PermanentAddress),
            TemporaryAddress = OptionalText(nameof(TemporaryAddress), TemporaryAddress, current.TemporaryAddress),
            BankName = OptionalText(nameof(BankName), BankName, current.BankName),
            BankAccountNumber = OptionalText(nameof(BankAccountNumber), BankAccountNumber, current.BankAccountNumber),
            TaxIdentifierKind = Text(nameof(TaxIdentifierKind), TaxIdentifierKind, current.TaxIdentifierKind),
            TaxIdentifier = Text(nameof(TaxIdentifier), TaxIdentifier, current.TaxIdentifier)
        };
        if (current.TaxIdentifierKind == "PERSONAL_ID" && merged.TaxIdentifierKind == "PERSONAL_ID" &&
            current.CccdNumber.Length > 0 && current.TaxIdentifier == current.CccdNumber &&
            Has(nameof(CccdNumber)) && !Has(nameof(TaxIdentifier)) && !Has(nameof(TaxIdentifierKind)))
            merged = merged with { TaxIdentifier = merged.CccdNumber };
        return merged;
    }

    private string Text(string property, string? value, string current)
    {
        if (!Has(property)) return current;
        if (string.IsNullOrWhiteSpace(value)) throw Required(property);
        return value.Trim();
    }

    private string OptionalText(string property, string? value, string current) =>
        Has(property) ? value?.Trim() ?? "" : current;

    private static ValidationException Required(string property) =>
        new([new ValidationFailure($"Details.{property}", "Trường này không được để trống.")]);
}

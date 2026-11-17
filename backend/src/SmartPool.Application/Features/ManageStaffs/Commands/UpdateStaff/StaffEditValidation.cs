using FluentValidation;
using FluentValidation.Results;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;

public static class StaffEditValidation
{
    public static async Task ValidateAsync(StaffLegalProfile profile, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (profile.TaxIdentifier.Length > 0 && profile.TaxIdentifierKind.Length == 0)
            throw new StaffOnboardingException("INVALID_TAX_IDENTIFIER_KIND", 400);
        var fields = new List<string> { nameof(profile.FullName), nameof(profile.Phone) };
        if (profile.DateOfBirth.HasValue) fields.Add(nameof(profile.DateOfBirth));
        if (profile.CccdNumber.Length > 0) fields.Add(nameof(profile.CccdNumber));
        if (profile.PermanentAddress.Length > 0) fields.Add(nameof(profile.PermanentAddress));
        if (profile.TemporaryAddress.Length > 0) fields.Add(nameof(profile.TemporaryAddress));
        if (profile.BankName.Length > 0) fields.Add(nameof(profile.BankName));
        if (profile.BankAccountNumber.Length > 0) fields.Add(nameof(profile.BankAccountNumber));
        if (profile.TaxIdentifierKind.Length > 0) fields.Add(nameof(profile.TaxIdentifierKind));
        if (profile.TaxIdentifier.Length > 0) fields.Add(nameof(profile.TaxIdentifier));

        var validation = await new StaffLegalProfileValidator(true, clock)
            .ValidateAsync(profile, options => options.IncludeProperties(fields.ToArray()), cancellationToken);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors.Select(error =>
                new ValidationFailure($"Details.{error.PropertyName}", "Giá trị không hợp lệ.")));
    }
}

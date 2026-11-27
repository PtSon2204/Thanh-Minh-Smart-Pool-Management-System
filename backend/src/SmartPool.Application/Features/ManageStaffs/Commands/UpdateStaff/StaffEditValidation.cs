using FluentValidation;
using FluentValidation.Results;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;

public static class StaffEditValidation
{
    public static async Task ValidateAsync(StaffLegalProfile profile, StaffEditDetails details, TimeProvider clock, CancellationToken cancellationToken)
    {
        if ((details.Has(nameof(details.TaxIdentifier)) || details.Has(nameof(details.TaxIdentifierKind))) && profile.TaxIdentifier.Length > 0 && profile.TaxIdentifierKind.Length == 0)
            throw new StaffOnboardingException("INVALID_TAX_IDENTIFIER_KIND", 400);
        var fields = new List<string>();
        foreach (var name in new[] { nameof(profile.FullName), nameof(profile.Phone), nameof(profile.DateOfBirth), nameof(profile.CccdNumber), nameof(profile.PermanentAddress), nameof(profile.TaxIdentifierKind), nameof(profile.TaxIdentifier) })
            if (details.Has(name)) fields.Add(name);
        if (details.Has(nameof(profile.TemporaryAddress)) && profile.TemporaryAddress.Length > 0) fields.Add(nameof(profile.TemporaryAddress));
        if (details.Has(nameof(profile.BankName)) && profile.BankName.Length > 0) fields.Add(nameof(profile.BankName));
        if (details.Has(nameof(profile.BankAccountNumber)) && profile.BankAccountNumber.Length > 0) fields.Add(nameof(profile.BankAccountNumber));
        if (fields.Count == 0) return;

        var validation = await new StaffLegalProfileValidator(true, clock)
            .ValidateAsync(profile, options => options.IncludeProperties(fields.ToArray()), cancellationToken);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors.Select(error =>
                new ValidationFailure($"Details.{error.PropertyName}", "Giá trị không hợp lệ.")));
    }
}

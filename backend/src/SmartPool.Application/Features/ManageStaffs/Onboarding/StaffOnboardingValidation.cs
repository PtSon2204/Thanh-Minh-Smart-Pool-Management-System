using FluentValidation;
using SmartPool.Application.Common.Time;

namespace SmartPool.Application.Features.ManageStaffs.Onboarding;

public static class StaffOnboardingValidation
{
    public static string Phone(string? value)
    {
        var phone = value?.Trim() ?? "";
        return phone.StartsWith("+84", StringComparison.Ordinal) ? "0" + phone[3..] : phone;
    }

    public static StaffLegalProfile Normalize(StaffLegalProfile profile) => profile with
    {
        FullName = profile.FullName?.Trim() ?? "", Phone = Phone(profile.Phone),
        CccdNumber = profile.CccdNumber?.Trim() ?? "",
        PermanentAddress = profile.PermanentAddress?.Trim() ?? "",
        TemporaryAddress = profile.TemporaryAddress?.Trim() ?? "",
        BankName = profile.BankName?.Trim() ?? "", BankAccountNumber = profile.BankAccountNumber?.Trim() ?? "",
        TaxIdentifierKind = profile.TaxIdentifierKind?.Trim() ?? "", TaxIdentifier = profile.TaxIdentifier?.Trim() ?? "",
        EmergencyContactName = profile.EmergencyContactName?.Trim() ?? "",
        EmergencyContactPhone = Phone(profile.EmergencyContactPhone),
        EmergencyContactRelationship = profile.EmergencyContactRelationship?.Trim() ?? ""
    };

    public static void CheckVersion(long actual, long? expected)
    {
        if (actual != expected) throw new StaffOnboardingException("STALE_VERSION");
    }

    public static void RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            throw new StaffOnboardingException("REASON_REQUIRED", 400);
    }

    public static void ValidateJoinDate(DateOnly? joinDate, TimeProvider clock)
    {
        if (joinDate is null || joinDate < VietnamTimeBoundary.GetDate(clock))
            throw new StaffOnboardingException("INVALID_JOIN_DATE", 400);
    }

    public static void ValidatePassword(string? password, string? confirmation)
    {
        if (password is null || password.Length < 8 || System.Text.Encoding.UTF8.GetByteCount(password) > 72 ||
            !password.Any(char.IsLetter) || !password.Any(char.IsDigit) || password != confirmation)
            throw new StaffOnboardingException("INVALID_PASSWORD", 400);
    }
}

public sealed class StaffInvitationProfileValidator : AbstractValidator<StaffLegalProfile>
{
    public StaffInvitationProfileValidator()
    {
        RuleFor(profile => profile.FullName).NotEmpty().MaximumLength(255);
        RuleFor(profile => profile.Phone).NotEmpty().Matches(@"^0[35789][0-9]{8}$");
    }
}

public sealed class StaffLegalProfileValidator : AbstractValidator<StaffLegalProfile>
{
    public StaffLegalProfileValidator() : this(true, TimeProvider.System) { }

    public StaffLegalProfileValidator(bool submitted, TimeProvider clock)
    {
        RuleFor(profile => profile.FullName).MaximumLength(255);
        RuleFor(profile => profile.Phone).MaximumLength(20);
        RuleFor(profile => profile.PermanentAddress).MaximumLength(500);
        RuleFor(profile => profile.TemporaryAddress).MaximumLength(500);
        RuleFor(profile => profile.BankName).MaximumLength(150);
        RuleFor(profile => profile.BankAccountNumber).MaximumLength(30);
        RuleFor(profile => profile.CccdNumber).MaximumLength(12);
        RuleFor(profile => profile.TaxIdentifier).MaximumLength(12);
        RuleFor(profile => profile.TaxIdentifierKind).Must(value => value is "PERSONAL_ID" or "LEGACY_TAX_CODE");
        RuleFor(profile => profile.EmergencyContactName).MaximumLength(255);
        RuleFor(profile => profile.EmergencyContactPhone).MaximumLength(20);
        RuleFor(profile => profile.EmergencyContactRelationship).MaximumLength(100);
        if (!submitted) return;
        RuleFor(profile => profile.FullName).NotEmpty();
        RuleFor(profile => profile.Phone).Matches(@"^0[35789][0-9]{8}$");
        RuleFor(profile => profile.DateOfBirth).NotNull()
            .Must(value => value is null || value.Value.Year <= VietnamTimeBoundary.GetDate(clock).Year - 18)
            .WithMessage("Nhân viên phải từ 18 tuổi, tính theo năm sinh.");
        RuleFor(profile => profile.CccdNumber).Matches("^[0-9]{12}$");
        RuleFor(profile => profile.PermanentAddress).NotEmpty();
        RuleFor(profile => profile.TemporaryAddress).NotEmpty();
        RuleFor(profile => profile.BankName).NotEmpty();
        RuleFor(profile => profile.BankAccountNumber).Matches("^[0-9]{6,30}$");
        When(profile => profile.TaxIdentifierKind == "PERSONAL_ID", () =>
            RuleFor(profile => profile.TaxIdentifier).Equal(profile => profile.CccdNumber));
        When(profile => profile.TaxIdentifierKind == "LEGACY_TAX_CODE", () =>
            RuleFor(profile => profile.TaxIdentifier).Matches("^[0-9]{10}$"));
        When(profile => profile.EmergencyContactName.Length + profile.EmergencyContactPhone.Length +
            profile.EmergencyContactRelationship.Length > 0, () =>
        {
            RuleFor(profile => profile.EmergencyContactName).NotEmpty();
            RuleFor(profile => profile.EmergencyContactPhone).Matches(@"^0[35789][0-9]{8}$");
        });
    }
}

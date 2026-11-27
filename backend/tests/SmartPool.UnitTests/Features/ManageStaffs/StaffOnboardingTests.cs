using System.Globalization;
using FluentValidation;
using SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffOnboardingTests
{
    [Fact]
    public void EditPreservesIndependentTaxAndSynchronizesOnlyMatchingPersonalId()
    {
        var original = new StaffLegalProfile { CccdNumber = "123456789012", TaxIdentifierKind = "PERSONAL_ID", TaxIdentifier = "123456789012", EmergencyContactName = "Legacy" };
        var correction = new StaffEditDetails { CccdNumber = "123456789013" };
        Assert.Equal("123456789013", correction.Merge(original).TaxIdentifier);
        Assert.Equal("Legacy", correction.Merge(original).EmergencyContactName);
        Assert.Equal("987654321098", correction.Merge(original with { TaxIdentifier = "987654321098" }).TaxIdentifier);
        Assert.Equal("123456789012", new StaffEditDetails { CccdNumber = "123456789013", TaxIdentifier = "123456789012" }.Merge(original).TaxIdentifier);
        Assert.Equal("", new StaffEditDetails { TemporaryAddress = null }.Merge(original with { TemporaryAddress = "old" }).TemporaryAddress);
        Assert.Equal("old", new StaffEditDetails().Merge(original with { TemporaryAddress = "old" }).TemporaryAddress);
    }
    [Fact]
    public void AgeUsesVietnamCalendarDateAndJoinDateRejectsThePast()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 12, 31, 18, 0, 0, TimeSpan.Zero));
        var validator = new StaffLegalProfileValidator(true, clock);
        bool ValidBirth(DateOnly birth) => !validator.Validate(new StaffLegalProfile { DateOfBirth = birth }).Errors
            .Any(error => error.PropertyName == nameof(StaffLegalProfile.DateOfBirth));
        Assert.True(ValidBirth(new DateOnly(2009, 1, 1)));
        Assert.False(ValidBirth(new DateOnly(2009, 1, 2)));
        Assert.Throws<StaffOnboardingException>(() => StaffOnboardingValidation.ValidateJoinDate(new DateOnly(2026, 12, 31), clock));
        Assert.Throws<StaffOnboardingException>(() => StaffOnboardingValidation.ValidateJoinDate(null, clock));
        StaffOnboardingValidation.ValidateJoinDate(new DateOnly(2027, 1, 1), clock);
        StaffOnboardingValidation.ValidateJoinDate(new DateOnly(2027, 1, 2), clock);
    }

    [Theory]
    [InlineData("2008-11-26", true)]
    [InlineData("2008-11-27", false)]
    [InlineData("1990-01-01", true)]
    [InlineData("2026-11-27", false)]
    public async Task DirectEmployeeCreationAndEditingEnforceInclusiveMinimumAge(string birth, bool accepted)
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));
        var dateOfBirth = DateOnly.ParseExact(birth, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var creation = new AddEmployeeValidator(clock).Validate(new AddEmployeeCommand { DateOfBirth = dateOfBirth });
        Assert.Equal(accepted, creation.Errors.All(error =>
            error.PropertyName != nameof(AddEmployeeCommand.DateOfBirth)));

        var profile = new StaffLegalProfile
        {
            FullName = "Nguyen Van A",
            Phone = "0912345678",
            DateOfBirth = dateOfBirth,
        };
        if (accepted)
        {
            await StaffEditValidation.ValidateAsync(profile, new StaffEditDetails { DateOfBirth = dateOfBirth }, clock, CancellationToken.None);
            return;
        }

        var failure = await Assert.ThrowsAsync<ValidationException>(() =>
            StaffEditValidation.ValidateAsync(profile, new StaffEditDetails { DateOfBirth = dateOfBirth }, clock, CancellationToken.None));
        Assert.Contains(failure.Errors, error => error.PropertyName == "Details.DateOfBirth");
    }

    [Fact]
    public void PasswordAndVersionBoundariesRejectUnsafeInput()
    {
        StaffOnboardingValidation.ValidatePassword("Synthetic123!", "Synthetic123!");
        Assert.Throws<StaffOnboardingException>(() => StaffOnboardingValidation.ValidatePassword("short", "short"));
        Assert.Throws<StaffOnboardingException>(() => StaffOnboardingValidation.CheckVersion(2, 1));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

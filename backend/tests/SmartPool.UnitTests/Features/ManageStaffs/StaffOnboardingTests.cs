using SmartPool.Application.Features.ManageStaffs.Onboarding;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using Microsoft.Extensions.DependencyInjection;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Interfaces.Services;
using System.Security.Claims;
using SmartPool.Infrastructure.StaffOnboarding;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffOnboardingTests
{
    [Fact]
    public void AgeUsesVietnamYearAndJoinDateRejectsThePast()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 12, 31, 18, 0, 0, TimeSpan.Zero));
        var validator = new StaffLegalProfileValidator(true, clock);
        bool ValidBirth(DateOnly birth) => !validator.Validate(new StaffLegalProfile { DateOfBirth = birth }).Errors
            .Any(error => error.PropertyName == nameof(StaffLegalProfile.DateOfBirth));
        Assert.True(ValidBirth(new DateOnly(2009, 12, 31)));
        Assert.False(ValidBirth(new DateOnly(2010, 1, 1)));
        Assert.Throws<StaffOnboardingException>(() => StaffOnboardingValidation.ValidateJoinDate(new DateOnly(2026, 12, 31), clock));
        Assert.Throws<StaffOnboardingException>(() => StaffOnboardingValidation.ValidateJoinDate(null, clock));
        StaffOnboardingValidation.ValidateJoinDate(new DateOnly(2027, 1, 1), clock);
        StaffOnboardingValidation.ValidateJoinDate(new DateOnly(2027, 1, 2), clock);
    }

    [Fact]
    public void InvitationEmailUsesVietnameseCopyAndEscapesTheLink()
    {
        var body = StaffInvitationEmail.Body("https://example.test/staff/onboarding#invitation=synthetic&token=test\"value");
        Assert.Equal("Thư mời hoàn thiện hồ sơ nhân viên | Thành Minh Smart Pool", StaffInvitationEmail.Subject);
        Assert.Contains("Kính gửi Anh/Chị,", body);
        Assert.Contains("Hoàn thiện hồ sơ nhân viên", body);
        Assert.Contains("24 giờ", body);
        Assert.Contains("&amp;token=test&quot;value", body);
        Assert.DoesNotContain("Complete your staff profile", body);
    }

    [Fact]
    public void InvitationRequiresNameAndVietnameseMobileButDraftCanRemainIncomplete()
    {
        var validator = new StaffInvitationProfileValidator();
        Assert.False(validator.Validate(new StaffLegalProfile()).IsValid);
        Assert.False(validator.Validate(new StaffLegalProfile { FullName = "   ", Phone = "0901234567" }).IsValid);
        Assert.False(validator.Validate(new StaffLegalProfile { FullName = "Synthetic staff", Phone = "0123456789" }).IsValid);
        Assert.False(validator.Validate(new StaffLegalProfile { FullName = new string('a', 256), Phone = "0901234567" }).IsValid);
        var normalized = StaffOnboardingValidation.Normalize(new StaffLegalProfile { FullName = " Synthetic staff ", Phone = " +84901234567 " });
        Assert.True(validator.Validate(normalized).IsValid);
        Assert.Equal("Synthetic staff", normalized.FullName);
        Assert.Equal("0901234567", normalized.Phone);
        Assert.True(new StaffLegalProfileValidator(false, TimeProvider.System).Validate(new StaffLegalProfile()).IsValid);
    }

    [Fact]
    public async Task ControllerRequiresTokenBoundaryAndMapsControlledConflicts()
    {
        var sender = new Mock<ISender>();
        var controller = new StaffsController(sender.Object) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<BadRequestObjectResult>(await controller.InspectOnboarding(new(), default));
        sender.Verify(item => item.Send(It.IsAny<OnboardingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        var approval = typeof(StaffsController).GetMethod(nameof(StaffsController.ApproveOnboarding))!;
        Assert.Contains(approval.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), item => item.Policy == AuthorizationPolicies.AdminOnly);
        Assert.IsType<UnauthorizedResult>(await controller.ApproveOnboarding(Guid.NewGuid(), new(), default));
        sender.Setup(item => item.Send(It.IsAny<OnboardingCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new StaffOnboardingException("STALE_VERSION"));
        var conflict = Assert.IsType<ObjectResult>(await controller.InspectOnboarding(new() { InvitationId = Guid.NewGuid(), Token = "synthetic" }, default));
        Assert.Equal(409, conflict.StatusCode);
        sender.Setup(item => item.Send(It.IsAny<CreateStaffCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new StaffOnboardingException("ONBOARDING_REQUIRED"));
        Assert.Equal(409, Assert.IsType<ObjectResult>(await controller.CreateStaff(new(), default)).StatusCode);
    }

    [Fact]
    public async Task InvitationAuthorizationAcceptsAdminCasingWithoutBypassingPermissions()
    {
        var reader = new Mock<IRolePermissionReader>();
        reader.Setup(item => item.ReadCurrentPermissionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"staff\":[\"read\"]}");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(reader.Object);
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var action = typeof(StaffsController).GetMethod(nameof(StaffsController.Invitations))!;
        var policy = await AuthorizationPolicy.CombineAsync(policies,
            action.GetCustomAttributes(true).OfType<IAuthorizeData>());
        Assert.NotNull(policy);
        ClaimsPrincipal Principal(string role) => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "synthetic-test"));
        foreach (var role in new[] { "ADMIN", "Admin", "admin" })
            Assert.True((await authorization.AuthorizeAsync(Principal(role), null, policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(Principal("SALES"), null, policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy)).Succeeded);
        reader.Setup(item => item.ReadCurrentPermissionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");
        Assert.False((await authorization.AuthorizeAsync(Principal("Admin"), null, policy)).Succeeded);
    }

    [Fact]
    public void DraftAllowsMissingFieldsButSubmissionRequiresLegalFields()
    {
        var draft = new StaffLegalProfile();
        Assert.True(new StaffLegalProfileValidator(false, TimeProvider.System).Validate(draft).IsValid);
        Assert.False(new StaffLegalProfileValidator(true, TimeProvider.System).Validate(draft).IsValid);
        var complete = new StaffLegalProfile { FullName = "Synthetic staff", Phone = "0901234567", DateOfBirth = new(2000, 1, 1),
            CccdNumber = "001234567890", PermanentAddress = "Synthetic address", TemporaryAddress = "Synthetic address",
            BankName = "Synthetic bank", BankAccountNumber = "000123456", TaxIdentifier = "001234567890" };
        var validator = new StaffLegalProfileValidator(true, TimeProvider.System);
        Assert.True(validator.Validate(complete).IsValid);
        Assert.False(validator.Validate(complete with { TaxIdentifier = "001234567891" }).IsValid);
        Assert.True(validator.Validate(complete with { TaxIdentifierKind = "LEGACY_TAX_CODE", TaxIdentifier = "0012345678" }).IsValid);
        Assert.False(validator.Validate(complete with { EmergencyContactRelationship = "Friend" }).IsValid);
    }

    [Fact]
    public void PasswordAndVersionBoundariesRejectUnsafeInput()
    {
        StaffOnboardingValidation.ValidatePassword("Synthetic123!", "Synthetic123!");
        Assert.Throws<StaffOnboardingException>(() => StaffOnboardingValidation.ValidatePassword("short", "short"));
        Assert.Throws<StaffOnboardingException>(() => StaffOnboardingValidation.ValidatePassword(new string('é', 40) + "1", new string('é', 40) + "1"));
        Assert.Throws<StaffOnboardingException>(() => StaffOnboardingValidation.CheckVersion(2, 1));
        Assert.Equal("0901234567", StaffOnboardingValidation.Phone(" +84901234567 "));
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

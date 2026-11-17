using FluentValidation.Results;
using SmartPool.Application.Features.ManageSwimClasses.Commands.DeactivateSwimClass;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class DeactivateSwimClassValidatorTests
{
    private readonly DeactivateSwimClassValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Lý do và phiên bản hợp lệ → không có lỗi")]
    public void Validate_ValidCommand_HasNoErrors() => Assert.True(_validator.Validate(Command()).IsValid);

    [Fact(DisplayName = "UTCID02 (A) - ClassId rỗng → lỗi ClassId")]
    public void Validate_EmptyClassId_HasError()
    {
        var command = Command();
        command.ClassId = Guid.Empty;
        AssertError(command, "ClassId");
    }

    [Fact(DisplayName = "UTCID03 (A) - Lý do rỗng → lỗi Reason")]
    public void Validate_EmptyReason_HasError() => AssertError(Command(reason: ""), "Reason");

    [Fact(DisplayName = "UTCID04 (A) - Lý do chỉ có khoảng trắng → lỗi Reason")]
    public void Validate_WhitespaceReason_HasError() => AssertError(Command(reason: "   "), "Reason");

    [Fact(DisplayName = "UTCID05 (B) - Lý do đúng 500 ký tự → hợp lệ")]
    public void Validate_ReasonAtMaxLength_IsValid() =>
        Assert.True(_validator.Validate(Command(reason: new string('a', 500))).IsValid);

    [Fact(DisplayName = "UTCID06 (B) - Lý do 501 ký tự → lỗi Reason")]
    public void Validate_ReasonTooLong_HasError() =>
        AssertError(Command(reason: new string('a', 501)), "Reason", "Lý do ngừng lớp phải có từ 1 đến 500 ký tự.");

    [Fact(DisplayName = "UTCID07 (A) - Thiếu ExpectedUpdatedAt → lỗi phiên bản")]
    public void Validate_MissingVersion_HasError()
    {
        var command = Command();
        command.ExpectedUpdatedAt = default;
        AssertError(command, "ExpectedUpdatedAt");
    }

    private void AssertError(DeactivateSwimClassCommand command, string property, string? message = null)
    {
        ValidationResult result = _validator.Validate(command);
        Assert.Contains(result.Errors, error => error.PropertyName == property &&
            (message is null || error.ErrorMessage == message));
    }

    private static DeactivateSwimClassCommand Command(string reason = "Hồ bơi bảo trì") => new()
    {
        ClassId = Guid.NewGuid(),
        Reason = reason,
        ExpectedUpdatedAt = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero)
    };
}

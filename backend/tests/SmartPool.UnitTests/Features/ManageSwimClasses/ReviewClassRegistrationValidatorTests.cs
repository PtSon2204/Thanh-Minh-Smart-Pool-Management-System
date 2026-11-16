using FluentValidation.Results;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ReviewClassRegistrationValidatorTests
{
    private readonly ReviewClassRegistrationValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Accepted hợp lệ không cần ghi chú → không có lỗi")]
    public void Validate_AcceptedWithoutNote_IsValid() => Assert.True(_validator.Validate(Command()).IsValid);

    [Fact(DisplayName = "UTCID02 (N) - Rejected có lý do → không có lỗi")]
    public void Validate_RejectedWithNote_IsValid() =>
        Assert.True(_validator.Validate(Command("Rejected", "Không đủ điều kiện")).IsValid);

    [Fact(DisplayName = "UTCID03 (A) - ClassId rỗng → lỗi ClassId")]
    public void Validate_EmptyClassId_HasError()
    {
        var command = Command();
        command.ClassId = Guid.Empty;
        AssertError(command, "ClassId");
    }

    [Fact(DisplayName = "UTCID04 (A) - RegistrationId rỗng → lỗi RegistrationId")]
    public void Validate_EmptyRegistrationId_HasError()
    {
        var command = Command();
        command.RegistrationId = Guid.Empty;
        AssertError(command, "RegistrationId");
    }

    [Fact(DisplayName = "UTCID05 (A) - Decision không phải Accepted/Rejected → lỗi Decision")]
    public void Validate_UnknownDecision_HasError() =>
        AssertError(Command("Pending"), "Decision", "Quyết định phải là Accepted hoặc Rejected.");

    [Fact(DisplayName = "UTCID06 (B) - Decision có khoảng trắng và khác hoa thường → hợp lệ")]
    public void Validate_CaseInsensitiveTrimmedDecision_IsValid() =>
        Assert.True(_validator.Validate(Command(" accepted ")).IsValid);

    [Fact(DisplayName = "UTCID07 (A) - Rejected không có lý do → lỗi ReviewNote")]
    public void Validate_RejectedWithoutNote_HasError() =>
        AssertError(Command("Rejected", "  "), "ReviewNote", "Từ chối đăng ký phải ghi lý do.");

    [Fact(DisplayName = "UTCID08 (B) - Ghi chú sau trim đúng 500 ký tự → hợp lệ")]
    public void Validate_NoteAtLimit_IsValid() =>
        Assert.True(_validator.Validate(Command("Rejected", " " + new string('a', 500) + " ")).IsValid);

    [Fact(DisplayName = "UTCID09 (B) - Ghi chú sau trim 501 ký tự → lỗi ReviewNote")]
    public void Validate_NoteTooLong_HasError() =>
        AssertError(Command("Accepted", new string('a', 501)), "ReviewNote", "Ghi chú duyệt tối đa 500 ký tự.");

    [Fact(DisplayName = "UTCID10 (A) - Thiếu ExpectedUpdatedAt → lỗi phiên bản")]
    public void Validate_MissingVersion_HasError()
    {
        var command = Command();
        command.ExpectedUpdatedAt = default;
        AssertError(command, "ExpectedUpdatedAt", "Cần thời gian cập nhật gần nhất của đăng ký.");
    }

    private void AssertError(ReviewClassRegistrationCommand command, string property, string? message = null)
    {
        ValidationResult result = _validator.Validate(command);
        Assert.Contains(result.Errors, error => error.PropertyName == property &&
            (message is null || error.ErrorMessage == message));
    }

    private static ReviewClassRegistrationCommand Command(string decision = "Accepted", string? note = null) => new()
    {
        ClassId = Guid.NewGuid(),
        RegistrationId = Guid.NewGuid(),
        Decision = decision,
        ReviewNote = note,
        ExpectedUpdatedAt = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero)
    };
}

using FluentValidation.Results;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ReviewClassAttendanceValidatorTests
{
    private readonly ReviewClassAttendanceValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Verified không cần ghi chú → hợp lệ")]
    public void Validate_VerifiedWithoutNote_IsValid() => Assert.True(_validator.Validate(Command()).IsValid);

    [Fact(DisplayName = "UTCID02 (N) - Rejected có lý do → hợp lệ")]
    public void Validate_RejectedWithNote_IsValid() =>
        Assert.True(_validator.Validate(Command("Rejected", "Ảnh không rõ")).IsValid);

    [Fact(DisplayName = "UTCID03 (A) - ClassId rỗng → lỗi ClassId")]
    public void Validate_EmptyClassId_HasError()
    {
        var command = Command(); command.ClassId = Guid.Empty;
        AssertError(command, "ClassId");
    }

    [Fact(DisplayName = "UTCID04 (A) - SessionId rỗng → lỗi SessionId")]
    public void Validate_EmptySessionId_HasError()
    {
        var command = Command(); command.SessionId = Guid.Empty;
        AssertError(command, "SessionId");
    }

    [Fact(DisplayName = "UTCID05 (A) - AttendanceId rỗng → lỗi AttendanceId")]
    public void Validate_EmptyAttendanceId_HasError()
    {
        var command = Command(); command.AttendanceId = Guid.Empty;
        AssertError(command, "AttendanceId");
    }

    [Fact(DisplayName = "UTCID06 (A) - Decision sai → lỗi Decision và thông báo")]
    public void Validate_UnknownDecision_HasError() =>
        AssertError(Command("PendingReview"), "Decision", "Kết quả phải là Verified hoặc Rejected.");

    [Fact(DisplayName = "UTCID07 (B) - Decision có khoảng trắng và khác hoa thường → hợp lệ")]
    public void Validate_TrimmedDecision_IsValid() =>
        Assert.True(_validator.Validate(Command(" verified ")).IsValid);

    [Fact(DisplayName = "UTCID08 (A) - Rejected thiếu lý do → lỗi ReviewNote")]
    public void Validate_RejectedWithoutNote_HasError() =>
        AssertError(Command("Rejected", " "), "ReviewNote", "Từ chối điểm danh phải ghi lý do.");

    [Fact(DisplayName = "UTCID09 (B) - Ghi chú sau trim đúng 500 ký tự → hợp lệ")]
    public void Validate_NoteAtLimit_IsValid() =>
        Assert.True(_validator.Validate(Command("Rejected", " " + new string('a', 500) + " ")).IsValid);

    [Fact(DisplayName = "UTCID10 (B) - Ghi chú sau trim 501 ký tự → lỗi ReviewNote")]
    public void Validate_NoteTooLong_HasError() =>
        AssertError(Command("Verified", new string('a', 501)), "ReviewNote", "Ghi chú duyệt tối đa 500 ký tự.");

    [Fact(DisplayName = "UTCID11 (A) - Thiếu ExpectedUpdatedAt → lỗi phiên bản")]
    public void Validate_MissingVersion_HasError()
    {
        var command = Command(); command.ExpectedUpdatedAt = default;
        AssertError(command, "ExpectedUpdatedAt", "Cần thời gian cập nhật gần nhất của điểm danh.");
    }

    private void AssertError(ReviewClassAttendanceCommand command, string property, string? message = null)
    {
        ValidationResult result = _validator.Validate(command);
        Assert.Contains(result.Errors, x => x.PropertyName == property &&
            (message is null || x.ErrorMessage == message));
    }

    private static ReviewClassAttendanceCommand Command(string decision = "Verified", string? note = null) => new()
    {
        ClassId = Guid.NewGuid(), SessionId = Guid.NewGuid(), AttendanceId = Guid.NewGuid(),
        Decision = decision, ReviewNote = note,
        ExpectedUpdatedAt = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero)
    };
}

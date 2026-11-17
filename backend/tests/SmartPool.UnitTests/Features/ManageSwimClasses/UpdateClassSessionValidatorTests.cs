using FluentValidation.Results;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class UpdateClassSessionValidatorTests
{
    private readonly UpdateClassSessionValidator _validator = new(new FixedClock());

    [Fact(DisplayName = "UTCID01 (N) - Buổi sáng và chiều chạm biên ca → hợp lệ")]
    public void Validate_ShiftBoundaries_HasNoErrors()
    {
        Assert.True(_validator.Validate(Command(6, 0, 11, 30)).IsValid);
        Assert.True(_validator.Validate(Command(13, 30, 19, 30)).IsValid);
    }

    [Fact(DisplayName = "UTCID02 (A) - ClassId rỗng → lỗi ClassId")]
    public void Validate_EmptyClassId_HasError()
    {
        var command = Command();
        command.ClassId = Guid.Empty;
        AssertError(command, "ClassId");
    }

    [Fact(DisplayName = "UTCID03 (A) - SessionId rỗng → lỗi SessionId")]
    public void Validate_EmptySessionId_HasError()
    {
        var command = Command();
        command.SessionId = Guid.Empty;
        AssertError(command, "SessionId");
    }

    [Fact(DisplayName = "UTCID04 (A) - Thiếu ExpectedUpdatedAt → lỗi phiên bản cập nhật")]
    public void Validate_MissingVersion_HasError()
    {
        var command = Command();
        command.ExpectedUpdatedAt = default;
        AssertError(command, "ExpectedUpdatedAt", "Cần thời gian cập nhật gần nhất của buổi học.");
    }

    [Fact(DisplayName = "UTCID05 (A) - StartsAt mặc định → lỗi giờ bắt đầu")]
    public void Validate_DefaultStart_HasError()
    {
        var command = Command();
        command.StartsAt = default;
        AssertError(command, "StartsAt", "Giờ bắt đầu không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID06 (A) - StartsAt trong quá khứ → lỗi buổi học phải ở tương lai")]
    public void Validate_PastStart_HasError() =>
        AssertError(Command(6, 0, 7, 0, day: 15), "StartsAt", "Buổi học phải bắt đầu trong tương lai.");

    [Fact(DisplayName = "UTCID07 (A) - EndsAt mặc định → lỗi giờ kết thúc")]
    public void Validate_DefaultEnd_HasError()
    {
        var command = Command();
        command.EndsAt = default;
        AssertError(command, "EndsAt", "Giờ kết thúc không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID08 (B) - EndsAt bằng StartsAt → lỗi thứ tự giờ")]
    public void Validate_EndEqualsStart_HasError() =>
        AssertError(Command(6, 0, 6, 0), "EndsAt", "Giờ kết thúc phải sau giờ bắt đầu.");

    [Fact(DisplayName = "UTCID09 (A) - EndsAt trước StartsAt → lỗi thứ tự giờ")]
    public void Validate_EndBeforeStart_HasError() =>
        AssertError(Command(7, 0, 6, 0), "EndsAt", "Giờ kết thúc phải sau giờ bắt đầu.");

    [Fact(DisplayName = "UTCID10 (A) - Buổi học qua ngày Việt Nam → lỗi cùng ngày")]
    public void Validate_CrossesVietnamDay_HasError()
    {
        var command = Command(19, 0, 19, 30);
        command.EndsAt = new DateTimeOffset(2026, 11, 21, 6, 30, 0, TimeSpan.FromHours(7));
        AssertError(command, "EndsAt", "Giờ kết thúc phải cùng ngày với giờ bắt đầu theo giờ Việt Nam.");
    }

    [Fact(DisplayName = "UTCID11 (B) - Bắt đầu trước 06:00 → lỗi ngoài ca")]
    public void Validate_BeforeMorningShift_HasError() =>
        AssertError(Command(5, 59, 7, 0), "EndsAt", ShiftMessage);

    [Fact(DisplayName = "UTCID12 (B) - Kéo qua giờ nghỉ trưa → lỗi ngoài ca")]
    public void Validate_CrossesLunchBreak_HasError() =>
        AssertError(Command(11, 15, 13, 45), "EndsAt", ShiftMessage);

    [Fact(DisplayName = "UTCID13 (B) - Kết thúc sau 19:30 → lỗi ngoài ca")]
    public void Validate_AfterAfternoonShift_HasError() =>
        AssertError(Command(18, 30, 19, 31), "EndsAt", ShiftMessage);

    private const string ShiftMessage =
        "Buổi học phải nằm trọn trong ca sáng 06:00–11:30 hoặc ca chiều 13:30–19:30 (giờ Việt Nam).";

    private void AssertError(UpdateClassSessionCommand command, string property, string? message = null)
    {
        ValidationResult result = _validator.Validate(command);
        Assert.Contains(result.Errors, error => error.PropertyName == property &&
            (message is null || error.ErrorMessage == message));
    }

    private static UpdateClassSessionCommand Command(int startHour = 6, int startMinute = 0,
        int endHour = 7, int endMinute = 0, int day = 20) => new()
        {
            ClassId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ExpectedUpdatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
            StartsAt = new DateTimeOffset(2026, 11, day, startHour, startMinute, 0, TimeSpan.FromHours(7)),
            EndsAt = new DateTimeOffset(2026, 11, day, endHour, endMinute, 0, TimeSpan.FromHours(7))
        };

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
    }
}

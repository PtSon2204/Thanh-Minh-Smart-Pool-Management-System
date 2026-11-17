using FluentValidation.Results;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CreateClassSessionsValidatorTests
{
    private readonly CreateClassSessionsValidator _validator = new(new FixedClock());

    [Fact(DisplayName = "UTCID01 (N) - Buổi học nằm trọn ca sáng/chiều, chạm biên giờ → hợp lệ")]
    public void Validate_ValidShiftBoundaries_HasNoErrors()
    {
        var morning = Session(6, 0, 11, 30);
        var afternoon = Session(13, 30, 19, 30);

        Assert.True(_validator.Validate(Command(morning, afternoon)).IsValid);
    }

    [Fact(DisplayName = "UTCID02 (A) - ClassId rỗng → lỗi ClassId")]
    public void Validate_EmptyClassId_HasError() =>
        AssertError(Command(Session(6, 0, 7, 0), classId: Guid.Empty), "ClassId");

    [Fact(DisplayName = "UTCID03 (A) - Không có buổi học → báo cần ít nhất một buổi")]
    public void Validate_NoSessions_HasError() =>
        AssertError(Command(), "Sessions", "Cần ít nhất một buổi học.");

    [Fact(DisplayName = "UTCID04 (A) - StartsAt mặc định → báo giờ bắt đầu không hợp lệ")]
    public void Validate_DefaultStart_HasError()
    {
        var session = Session(6, 0, 7, 0);
        session.StartsAt = default;
        AssertError(Command(session), "Sessions[0].StartsAt", "Giờ bắt đầu không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID05 (A) - Buổi học trong quá khứ → báo phải bắt đầu tương lai")]
    public void Validate_PastStart_HasError()
    {
        var session = Session(6, 0, 7, 0, day: 15);
        AssertError(Command(session), "Sessions[0].StartsAt", "Buổi học phải bắt đầu trong tương lai.");
    }

    [Fact(DisplayName = "UTCID06 (A) - EndsAt mặc định → báo giờ kết thúc không hợp lệ")]
    public void Validate_DefaultEnd_HasError()
    {
        var session = Session(6, 0, 7, 0);
        session.EndsAt = default;
        AssertError(Command(session), "Sessions[0].EndsAt", "Giờ kết thúc không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID07 (B) - EndsAt bằng StartsAt → báo thứ tự giờ")]
    public void Validate_EndEqualsStart_HasError()
    {
        var session = Session(6, 0, 6, 0);
        AssertError(Command(session), "Sessions[0].EndsAt", "Giờ kết thúc phải sau giờ bắt đầu.");
    }

    [Fact(DisplayName = "UTCID08 (A) - Buổi học qua ngày Việt Nam → báo không cùng ngày")]
    public void Validate_CrossesVietnamDay_HasError()
    {
        var session = Session(19, 0, 19, 30);
        session.EndsAt = new DateTimeOffset(2026, 11, 21, 6, 30, 0, TimeSpan.FromHours(7));
        AssertError(Command(session), "Sessions[0].EndsAt", "Giờ kết thúc phải cùng ngày với giờ bắt đầu theo giờ Việt Nam.");
    }

    [Fact(DisplayName = "UTCID09 (B) - Bắt đầu trước 06:00 → báo ngoài ca")]
    public void Validate_BeforeMorningShift_HasError() =>
        AssertError(Command(Session(5, 59, 7, 0)), "Sessions[0].EndsAt", ShiftMessage);

    [Fact(DisplayName = "UTCID10 (B) - Buổi học kéo qua giờ nghỉ trưa → báo ngoài ca")]
    public void Validate_CrossesLunchBreak_HasError() =>
        AssertError(Command(Session(11, 15, 13, 45)), "Sessions[0].EndsAt", ShiftMessage);

    [Fact(DisplayName = "UTCID11 (B) - Kết thúc sau 19:30 → báo ngoài ca")]
    public void Validate_AfterAfternoonShift_HasError() =>
        AssertError(Command(Session(18, 30, 19, 31)), "Sessions[0].EndsAt", ShiftMessage);

    [Fact(DisplayName = "UTCID12 (A) - CoachUserId = Guid.Empty → báo mã Coach không hợp lệ")]
    public void Validate_EmptyCoachId_HasError()
    {
        var session = Session(6, 0, 7, 0);
        session.CoachUserId = Guid.Empty;
        AssertError(Command(session), "Sessions[0].CoachUserId", "Mã Coach không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID13 (A) - Buổi thứ hai sai → lỗi có chỉ số Sessions[1]")]
    public void Validate_SecondSessionError_ReportsCorrectIndex()
    {
        var second = Session(13, 30, 14, 30);
        second.CoachUserId = Guid.Empty;
        AssertError(Command(Session(6, 0, 7, 0), second), "Sessions[1].CoachUserId", "Mã Coach không hợp lệ.");
    }

    private const string ShiftMessage = "Buổi học phải nằm trọn trong ca sáng 06:00–11:30 hoặc ca chiều 13:30–19:30 (giờ Việt Nam).";

    private void AssertError(CreateClassSessionsCommand command, string property, string? message = null)
    {
        ValidationResult result = _validator.Validate(command);
        Assert.Contains(result.Errors, error => error.PropertyName == property &&
            (message is null || error.ErrorMessage == message));
    }

    private static CreateClassSessionsCommand Command(params CreateClassSessionItem[] sessions) =>
        Command(sessions, Guid.NewGuid());

    private static CreateClassSessionsCommand Command(CreateClassSessionItem session, Guid classId) =>
        Command([session], classId);

    private static CreateClassSessionsCommand Command(CreateClassSessionItem[] sessions, Guid classId) =>
        new() { ClassId = classId, Sessions = [.. sessions] };

    private static CreateClassSessionItem Session(int startHour, int startMinute, int endHour, int endMinute,
        int day = 20) => new()
        {
            StartsAt = new DateTimeOffset(2026, 11, day, startHour, startMinute, 0, TimeSpan.FromHours(7)),
            EndsAt = new DateTimeOffset(2026, 11, day, endHour, endMinute, 0, TimeSpan.FromHours(7))
        };

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
    }
}

using FluentValidation.Results;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CreateSwimClassValidatorTests
{
    private readonly CreateSwimClassValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Dữ liệu lớp hợp lệ → không có lỗi")]
    public void Validate_ValidClass_HasNoErrors() => Assert.True(_validator.Validate(Request()).IsValid);

    [Fact(DisplayName = "UTCID02 (A) - Tên lớp rỗng → báo lỗi Name")]
    public void Validate_EmptyName_HasError() =>
        AssertError(Request(name: "   "), "Name", "Tên lớp không được để trống.");

    [Fact(DisplayName = "UTCID03 (B) - Tên lớp sau trim 150 ký tự → hợp lệ")]
    public void Validate_NameAtMaximum_HasNoErrors() =>
        Assert.True(_validator.Validate(Request(name: " " + new string('a', 150) + " ")).IsValid);

    [Fact(DisplayName = "UTCID04 (B) - Tên lớp sau trim 151 ký tự → báo lỗi")]
    public void Validate_NameAboveMaximum_HasError() =>
        AssertError(Request(name: new string('a', 151)), "Name", "Tên lớp không được vượt quá 150 ký tự.");

    [Fact(DisplayName = "UTCID05 (A) - Sức chứa 0 → báo lỗi Capacity")]
    public void Validate_ZeroCapacity_HasError() =>
        AssertError(Request(capacity: 0), "Capacity", "Sức chứa phải từ 1 đến 5 học viên.");

    [Fact(DisplayName = "UTCID06 (B) - Sức chứa 1 và 5 → đều hợp lệ")]
    public void Validate_CapacityBoundaries_HaveNoErrors()
    {
        Assert.True(_validator.Validate(Request(capacity: 1)).IsValid);
        Assert.True(_validator.Validate(Request(capacity: 5)).IsValid);
    }

    [Fact(DisplayName = "UTCID07 (B) - Sức chứa 6 → báo lỗi Capacity")]
    public void Validate_CapacityAboveMaximum_HasError() =>
        AssertError(Request(capacity: 6), "Capacity", "Sức chứa phải từ 1 đến 5 học viên.");

    [Fact(DisplayName = "UTCID08 (A) - Học phí âm → báo lỗi Fee")]
    public void Validate_NegativeFee_HasError() =>
        AssertError(Request(fee: -1m), "Fee", "Học phí không được âm.");

    [Fact(DisplayName = "UTCID09 (B) - Học phí có 3 chữ số thập phân → báo lỗi Fee")]
    public void Validate_TooManyFractionDigits_HasError() =>
        AssertError(Request(fee: 123.456m), "Fee", "Học phí tối đa 18 chữ số, gồm 2 chữ số thập phân.");

    [Fact(DisplayName = "UTCID10 (A) - Thời gian bắt đầu đăng ký mặc định → báo lỗi")]
    public void Validate_DefaultRegistrationStart_HasError() =>
        AssertError(Request(start: default(DateTimeOffset)), "RegistrationStartAt", "Thời gian bắt đầu đăng ký không hợp lệ.");

    [Fact(DisplayName = "UTCID11 (A) - Thời gian kết thúc đăng ký mặc định → báo lỗi")]
    public void Validate_DefaultRegistrationEnd_HasError() =>
        AssertError(Request(end: default(DateTimeOffset)), "RegistrationEndAt", "Thời gian kết thúc đăng ký không hợp lệ.");

    [Fact(DisplayName = "UTCID12 (B) - Thời gian kết thúc bằng bắt đầu → báo lỗi thứ tự")]
    public void Validate_RegistrationEndEqualsStart_HasError()
    {
        var date = new DateTimeOffset(2026, 11, 20, 6, 0, 0, TimeSpan.FromHours(7));
        AssertError(Request(start: date, end: date), "RegistrationEndAt", "Thời gian kết thúc đăng ký phải sau thời gian bắt đầu.");
    }

    private void AssertError(CreateSwimClassCommand command, string property, string message)
    {
        ValidationResult result = _validator.Validate(command);
        Assert.Contains(result.Errors, error => error.PropertyName == property && error.ErrorMessage == message);
    }

    private static CreateSwimClassCommand Request(string name = "Lớp bơi", int capacity = 5,
        decimal fee = 100000m, DateTimeOffset? start = null, DateTimeOffset? end = null) => new()
    {
        Name = name, Capacity = capacity, Fee = fee,
        RegistrationStartAt = start ?? new DateTimeOffset(2026, 11, 20, 6, 0, 0, TimeSpan.FromHours(7)),
        RegistrationEndAt = end ?? new DateTimeOffset(2026, 11, 21, 18, 0, 0, TimeSpan.FromHours(7))
    };
}

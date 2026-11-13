using FluentValidation.Results;
using SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;

namespace SmartPool.UnitTests.Features.ManageUsers;

public sealed class UpdateUserValidatorTests
{
    private readonly UpdateUserValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Input hợp lệ → không có lỗi")]
    public async Task Validate_ValidRequest_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Request())).IsValid);

    [Fact(DisplayName = "UTCID02 (A) - Id rỗng → báo lỗi Id")]
    public async Task Validate_EmptyId_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(id: Guid.Empty)), "Id", "Mã người dùng không hợp lệ.");

    [Fact(DisplayName = "UTCID03 (A) - Email rỗng → báo lỗi Email")]
    public async Task Validate_EmptyEmail_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(email: "")), "Email", "Email không được để trống.");

    [Fact(DisplayName = "UTCID04 (A) - Email sai định dạng → báo lỗi Email")]
    public async Task Validate_InvalidEmail_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(email: "user..name@example.com")), "Email", "Email không đúng định dạng.");

    [Fact(DisplayName = "UTCID05 (B) - Email đúng 150 ký tự → hợp lệ")]
    public async Task Validate_EmailAtMaximum_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Request(email: new string('a', 138) + "@example.com"))).IsValid);

    [Fact(DisplayName = "UTCID06 (B) - Email 151 ký tự → báo lỗi độ dài")]
    public async Task Validate_EmailAboveMaximum_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(email: new string('a', 139) + "@example.com")),
            "Email", "Email không được vượt quá 150 ký tự.");

    [Fact(DisplayName = "UTCID07 (A) - Phone rỗng → báo lỗi Phone")]
    public async Task Validate_EmptyPhone_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(phone: "")), "Phone", "Số điện thoại không được để trống.");

    [Fact(DisplayName = "UTCID08 (A) - Phone sai dạng → báo lỗi Phone")]
    public async Task Validate_InvalidPhone_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(phone: "0212345678")), "Phone",
            "Số điện thoại phải là số di động Việt Nam dạng 0xxxxxxxxx hoặc +84xxxxxxxxx.");

    [Fact(DisplayName = "UTCID09 (B) - Phone dạng 0... và +84... → đều hợp lệ")]
    public async Task Validate_BothPhoneFormats_HaveNoErrors()
    {
        Assert.True((await _validator.ValidateAsync(Request(phone: "0912345678"))).IsValid);
        Assert.True((await _validator.ValidateAsync(Request(phone: "+84912345678"))).IsValid);
    }

    [Fact(DisplayName = "UTCID10 (A) - FullName chỉ có khoảng trắng → báo lỗi")]
    public async Task Validate_BlankFullName_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(fullName: "   ")), "FullName", "Họ tên không được để trống.");

    [Fact(DisplayName = "UTCID11 (B) - FullName sau trim đúng 255 ký tự → hợp lệ")]
    public async Task Validate_FullNameAtMaximum_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Request(fullName: " " + new string('a', 255) + " "))).IsValid);

    [Fact(DisplayName = "UTCID12 (B) - FullName sau trim 256 ký tự → báo lỗi")]
    public async Task Validate_FullNameAboveMaximum_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(fullName: " " + new string('a', 256) + " ")),
            "FullName", "Họ tên không được vượt quá 255 ký tự.");

    [Fact(DisplayName = "UTCID13 (A) - Ngày sinh ở tương lai → báo lỗi DateOfBirth")]
    public async Task Validate_FutureBirthDate_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(dateOfBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)))),
            "DateOfBirth", "Ngày sinh không được ở tương lai.");

    [Fact(DisplayName = "UTCID14 (B) - Ngày sinh và địa chỉ null → hợp lệ")]
    public async Task Validate_OptionalFieldsNull_HaveNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Request(address: null))).IsValid);

    [Fact(DisplayName = "UTCID15 (B) - Address sau trim đúng 500 ký tự → hợp lệ")]
    public async Task Validate_AddressAtMaximum_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Request(address: " " + new string('a', 500) + " "))).IsValid);

    [Fact(DisplayName = "UTCID16 (B) - Address sau trim 501 ký tự → báo lỗi")]
    public async Task Validate_AddressAboveMaximum_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(address: " " + new string('a', 501) + " ")),
            "Address", "Địa chỉ không được vượt quá 500 ký tự.");

    private static void AssertError(ValidationResult result, string property, string message) =>
        Assert.Contains(result.Errors, error => error.PropertyName == property && error.ErrorMessage == message);

    private static UpdateUserCommand Request(Guid? id = null, string email = "user@example.com",
        string phone = "0912345678", string fullName = "Nguyen Van A", DateOnly? dateOfBirth = null,
        string? address = "Pool Street") => new()
        {
            Id = id ?? Guid.NewGuid(), Email = email, Phone = phone, FullName = fullName,
            DateOfBirth = dateOfBirth, Address = address
        };
}

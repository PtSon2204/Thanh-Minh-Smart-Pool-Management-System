using FluentValidation.Results;
using SmartPool.Application.Features.ManageUsers.Commands.CreateUser;

namespace SmartPool.UnitTests.Features.ManageUsers;

public sealed class CreateUserValidatorTests
{
    private readonly CreateUserValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Dữ liệu tạo user hợp lệ → không có lỗi")]
    public async Task Validate_ValidRequest_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Request())).IsValid);

    [Fact(DisplayName = "UTCID02 (A) - Username rỗng → báo lỗi Username")]
    public async Task Validate_EmptyUsername_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(username: "")), "Username", "Tên đăng nhập không được để trống.");

    [Fact(DisplayName = "UTCID03 (B) - Username dài 2 ký tự → báo lỗi độ dài")]
    public async Task Validate_ShortUsername_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(username: "ab")), "Username", "Tên đăng nhập phải dài từ 3 đến 100 ký tự.");

    [Fact(DisplayName = "UTCID04 (B) - Username đúng 100 ký tự → hợp lệ")]
    public async Task Validate_UsernameAtMaximum_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Request(username: new string('a', 100)))).IsValid);

    [Fact(DisplayName = "UTCID05 (B) - Username 101 ký tự → báo lỗi độ dài")]
    public async Task Validate_UsernameAboveMaximum_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(username: new string('a', 101))),
            "Username", "Tên đăng nhập không được vượt quá 100 ký tự.");

    [Fact(DisplayName = "UTCID06 (A) - Username chứa ký tự không hợp lệ → báo lỗi")]
    public async Task Validate_InvalidUsernameCharacters_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(username: "user name")), "Username", "Tên đăng nhập không hợp lệ.");

    [Fact(DisplayName = "UTCID07 (A) - Họ tên rỗng → báo lỗi FullName")]
    public async Task Validate_EmptyFullName_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(fullName: "")), "FullName", "Họ tên không được để trống.");

    [Fact(DisplayName = "UTCID08 (B) - Họ tên 256 ký tự → báo lỗi độ dài")]
    public async Task Validate_FullNameAboveMaximum_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(fullName: new string('a', 256))),
            "FullName", "Họ tên không được vượt quá 255 ký tự.");

    [Fact(DisplayName = "UTCID09 (A) - Role không hỗ trợ → báo lỗi Role")]
    public async Task Validate_UnsupportedRole_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(role: "MANAGER")), "Role",
            "Vai trò phải là CUSTOMER, LIFEGUARD, SALES, COACH hoặc ADMIN.");

    [Fact(DisplayName = "UTCID10 (B) - Role có khoảng trắng và chữ thường → hợp lệ")]
    public async Task Validate_RoleCaseAndWhitespace_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Request(role: " customer "))).IsValid);

    [Fact(DisplayName = "UTCID13 (A) - Ngày sinh ở tương lai → báo lỗi DateOfBirth")]
    public async Task Validate_FutureBirthDate_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(dateOfBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)))),
            "DateOfBirth", "Ngày sinh không được ở tương lai.");

    [Fact(DisplayName = "UTCID14 (B) - Địa chỉ sau trim dài 501 ký tự → báo lỗi")]
    public async Task Validate_AddressAboveMaximum_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(address: " " + new string('a', 501) + " ")),
            "Address", "Địa chỉ không được vượt quá 500 ký tự.");

    [Fact(DisplayName = "UTCID15 (A) - Email sai định dạng → dùng đúng lỗi từ RegisterValidator")]
    public async Task Validate_InvalidEmail_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(email: "bad-email")), "Email", "Email không đúng định dạng.");

    [Fact(DisplayName = "UTCID16 (A) - Phone sai định dạng → dùng đúng lỗi từ RegisterValidator")]
    public async Task Validate_InvalidPhone_HasError() =>
        AssertError(await _validator.ValidateAsync(Request(phone: "0212345678")), "Phone",
            "Số điện thoại phải là số di động Việt Nam dạng 0xxxxxxxxx hoặc +84xxxxxxxxx.");

    private static void AssertError(ValidationResult result, string property, string message) =>
        Assert.Contains(result.Errors, error => error.PropertyName == property && error.ErrorMessage == message);

    private static CreateUserCommand Request(string username = "customer01", string email = "customer@example.com",
        string phone = "0912345678",
        string fullName = "Nguyen Van A", string role = "CUSTOMER", DateOnly? dateOfBirth = null,
        string? address = "Pool Street") => new()
        {
            Username = username, Email = email, Phone = phone,
            FullName = fullName, Role = role,
            DateOfBirth = dateOfBirth, Address = address
        };
}

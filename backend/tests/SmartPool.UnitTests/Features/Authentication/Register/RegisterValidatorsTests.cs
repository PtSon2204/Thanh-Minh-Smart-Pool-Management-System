using FluentValidation.Results;
using SmartPool.Application.Features.Authentication.Commands.Register;

namespace SmartPool.UnitTests.Features.Authentication.Register;

public sealed class RegisterValidatorsTests
{
    private const string PhoneFormatMessage =
        "Số điện thoại phải là số di động Việt Nam dạng 0xxxxxxxxx hoặc +84xxxxxxxxx.";
    private const string PasswordLengthMessage = "Mật khẩu phải dài từ 8 đến 128 ký tự.";
    private const string PasswordComplexityMessage = "Mật khẩu phải có ít nhất một chữ cái và một chữ số.";

    [Fact(DisplayName = "UTCID01 (A) - Email rỗng -> Báo Email không được để trống")]
    public void Validate_EmptyEmail_FailsValidation()
    {
        var result = new RegisterValidator().Validate(Register(email: string.Empty));

        AssertFieldError(result, nameof(RegisterCommand.Email), "Email không được để trống.");
    }

    [Fact(DisplayName = "UTCID02 (A) - Email sai định dạng -> Báo Email không đúng định dạng")]
    public void Validate_InvalidEmail_FailsValidation()
    {
        var result = new RegisterValidator().Validate(Register(email: "user..name@example.com"));

        AssertFieldError(result, nameof(RegisterCommand.Email), "Email không đúng định dạng.");
    }

    [Fact(DisplayName = "UTCID03 (B) - Email vượt 150 ký tự -> Báo vượt giới hạn")]
    public void Validate_EmailOver150Characters_FailsValidation()
    {
        var email = new string('a', 140) + "@example.com";
        Assert.True(email.Length > 150);

        var result = new RegisterValidator().Validate(Register(email: email));

        AssertFieldError(result, nameof(RegisterCommand.Email), "Email không được vượt quá 150 ký tự.");
    }

    [Fact(DisplayName = "UTCID04 (N) - Email hợp lệ -> Validator chấp nhận")]
    public void Validate_ValidEmail_PassesValidation()
    {
        var result = new RegisterValidator().Validate(Register(email: "User.Name+tag@example.com"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "UTCID05 (A) - Số điện thoại rỗng -> Báo không được để trống")]
    public void Validate_EmptyPhone_FailsValidation()
    {
        var result = new RegisterValidator().Validate(Register(phone: string.Empty));

        AssertFieldError(result, nameof(RegisterCommand.Phone), "Số điện thoại không được để trống.");
    }

    [Fact(DisplayName = "UTCID06 (A) - Số điện thoại sai dạng -> Báo đúng định dạng di động Việt Nam")]
    public void Validate_InvalidPhone_FailsValidation()
    {
        var result = new RegisterValidator().Validate(Register(phone: "0212345678"));

        AssertFieldError(result, nameof(RegisterCommand.Phone), PhoneFormatMessage);
    }

    [Fact(DisplayName = "UTCID07 (N) - Số điện thoại dạng 0xxxxxxxxx -> Validator chấp nhận")]
    public void Validate_LocalPhone_PassesValidation()
    {
        var result = new RegisterValidator().Validate(Register(phone: "0912345678"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "UTCID08 (N) - Số điện thoại dạng +84xxxxxxxxx -> Validator chấp nhận")]
    public void Validate_InternationalPhone_PassesValidation()
    {
        var result = new RegisterValidator().Validate(Register(phone: "+84912345678"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "UTCID09 (A) - Mật khẩu rỗng -> Báo không được để trống")]
    public void Validate_EmptyPassword_FailsValidation()
    {
        var result = new RegisterValidator().Validate(Register(password: string.Empty));

        AssertFieldError(result, nameof(RegisterCommand.Password), "Mật khẩu không được để trống.");
    }

    [Fact(DisplayName = "UTCID10 (B) - Mật khẩu ngắn hơn 8 ký tự -> Báo sai độ dài")]
    public void Validate_ShortPassword_FailsValidation()
    {
        var result = new RegisterValidator().Validate(Register(password: "Abc1234"));

        AssertFieldError(result, nameof(RegisterCommand.Password), PasswordLengthMessage);
    }

    [Fact(DisplayName = "UTCID11 (B) - Mật khẩu dài hơn 128 ký tự -> Báo sai độ dài")]
    public void Validate_LongPassword_FailsValidation()
    {
        var result = new RegisterValidator().Validate(Register(password: new string('A', 128) + "1"));

        AssertFieldError(result, nameof(RegisterCommand.Password), PasswordLengthMessage);
    }

    [Fact(DisplayName = "UTCID12 (A) - Mật khẩu không có chữ cái -> Báo thiếu chữ hoặc số")]
    public void Validate_PasswordWithoutLetter_FailsValidation()
    {
        var result = new RegisterValidator().Validate(Register(password: "12345678"));

        AssertFieldError(result, nameof(RegisterCommand.Password), PasswordComplexityMessage);
    }

    [Fact(DisplayName = "UTCID13 (A) - Mật khẩu không có chữ số -> Báo thiếu chữ hoặc số")]
    public void Validate_PasswordWithoutDigit_FailsValidation()
    {
        var result = new RegisterValidator().Validate(Register(password: "Password"));

        AssertFieldError(result, nameof(RegisterCommand.Password), PasswordComplexityMessage);
    }

    [Fact(DisplayName = "UTCID14 (A) - Xác minh với RegistrationId rỗng -> Báo mã phiên không hợp lệ")]
    public void Validate_EmptyVerifyRegistrationId_FailsValidation()
    {
        var result = new VerifyRegistrationValidator().Validate(new VerifyRegistrationCommand
        {
            RegistrationId = Guid.Empty,
            Otp = "012345"
        });

        AssertFieldError(result, nameof(VerifyRegistrationCommand.RegistrationId),
            "Mã phiên đăng ký không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID15 (A) - Gửi lại với RegistrationId rỗng -> Báo mã phiên không hợp lệ")]
    public void Validate_EmptyResendRegistrationId_FailsValidation()
    {
        var result = new ResendRegistrationValidator().Validate(new ResendRegistrationCommand
        {
            RegistrationId = Guid.Empty
        });

        AssertFieldError(result, nameof(ResendRegistrationCommand.RegistrationId),
            "Mã phiên đăng ký không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID16 (B) - OTP có 5 chữ số -> Báo sai định dạng")]
    public void Validate_ShortOtp_FailsValidation() => AssertInvalidOtp("12345");

    [Fact(DisplayName = "UTCID17 (B) - OTP có 7 chữ số -> Báo sai định dạng")]
    public void Validate_LongOtp_FailsValidation() => AssertInvalidOtp("1234567");

    [Fact(DisplayName = "UTCID18 (A) - OTP chứa chữ cái -> Báo sai định dạng")]
    public void Validate_NonDigitOtp_FailsValidation() => AssertInvalidOtp("12A456");

    private static void AssertInvalidOtp(string otp)
    {
        var result = new VerifyRegistrationValidator().Validate(new VerifyRegistrationCommand
        {
            RegistrationId = Guid.NewGuid(),
            Otp = otp
        });

        AssertFieldError(result, nameof(VerifyRegistrationCommand.Otp), "OTP phải gồm đúng 6 chữ số.");
    }

    [Fact(DisplayName = "UTCID19 (N) - OTP đúng 6 chữ số -> Validator chấp nhận")]
    public void Validate_ValidOtp_PassesValidation()
    {
        var result = new VerifyRegistrationValidator().Validate(new VerifyRegistrationCommand
        {
            RegistrationId = Guid.NewGuid(),
            Otp = "012345"
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static RegisterCommand Register(string email = "user@example.com",
        string phone = "0912345678", string password = "Password123") => new()
    {
        Email = email,
        Phone = phone,
        Password = password
    };

    private static void AssertFieldError(ValidationResult result, string propertyName, string message)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == propertyName && error.ErrorMessage == message);
    }
}

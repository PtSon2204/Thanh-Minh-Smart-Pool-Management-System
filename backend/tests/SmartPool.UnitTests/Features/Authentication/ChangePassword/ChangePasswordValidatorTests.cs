using FluentValidation.TestHelper;
using SmartPool.Application.Features.Authentication.Commands.ChangePassword;

namespace SmartPool.UnitTests.Features.Authentication.ChangePassword;

public sealed class ChangePasswordValidatorTests
{
    private readonly ChangePasswordValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Mật khẩu mới hợp lệ → không có lỗi")]
    public async Task Validate_ValidRequest_HasNoErrors() =>
        (await _validator.TestValidateAsync(Command())).ShouldNotHaveAnyValidationErrors();

    [Fact(DisplayName = "UTCID02 (A) - Mật khẩu hiện tại rỗng → báo lỗi đúng trường")]
    public async Task Validate_EmptyCurrentPassword_HasError() =>
        (await _validator.TestValidateAsync(Command(current: "")))
            .ShouldHaveValidationErrorFor(x => x.CurrentPassword)
            .WithErrorMessage("Vui lòng nhập mật khẩu hiện tại.");

    [Fact(DisplayName = "UTCID03 (B) - Mật khẩu hiện tại chỉ có khoảng trắng → báo lỗi")]
    public async Task Validate_WhitespaceCurrentPassword_HasError() =>
        (await _validator.TestValidateAsync(Command(current: "   ")))
            .ShouldHaveValidationErrorFor(x => x.CurrentPassword)
            .WithErrorMessage("Vui lòng nhập mật khẩu hiện tại.");

    [Fact(DisplayName = "UTCID04 (A) - Mật khẩu mới rỗng → báo lỗi đúng trường")]
    public async Task Validate_EmptyNewPassword_HasError() =>
        (await _validator.TestValidateAsync(Command(next: "", confirm: "")))
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Vui lòng nhập mật khẩu mới.");

    [Fact(DisplayName = "UTCID05 (B) - Mật khẩu mới 7 ký tự → báo lỗi độ dài")]
    public async Task Validate_SevenCharacterNewPassword_HasError() =>
        (await _validator.TestValidateAsync(Command(next: "Abc1234", confirm: "Abc1234")))
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Mật khẩu phải dài từ 8 đến 128 ký tự.");

    [Fact(DisplayName = "UTCID06 (B) - Mật khẩu mới đúng 8 ký tự → hợp lệ")]
    public async Task Validate_EightCharacterNewPassword_HasNoErrors() =>
        (await _validator.TestValidateAsync(Command(next: "Abcd1234", confirm: "Abcd1234")))
            .ShouldNotHaveAnyValidationErrors();

    [Fact(DisplayName = "UTCID07 (B) - Mật khẩu mới đúng 128 ký tự → hợp lệ")]
    public async Task Validate_128CharacterNewPassword_HasNoErrors()
    {
        var password = "A1" + new string('x', 126);
        (await _validator.TestValidateAsync(Command(next: password, confirm: password)))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact(DisplayName = "UTCID08 (B) - Mật khẩu mới 129 ký tự → báo lỗi độ dài")]
    public async Task Validate_129CharacterNewPassword_HasError()
    {
        var password = "A1" + new string('x', 127);
        (await _validator.TestValidateAsync(Command(next: password, confirm: password)))
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Mật khẩu phải dài từ 8 đến 128 ký tự.");
    }

    [Fact(DisplayName = "UTCID09 (A) - Mật khẩu mới thiếu chữ → báo lỗi thành phần")]
    public async Task Validate_NewPasswordWithoutLetter_HasError() =>
        (await _validator.TestValidateAsync(Command(next: "12345678", confirm: "12345678")))
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Mật khẩu phải có ít nhất một chữ cái và một chữ số.");

    [Fact(DisplayName = "UTCID10 (A) - Mật khẩu mới thiếu số → báo lỗi thành phần")]
    public async Task Validate_NewPasswordWithoutDigit_HasError() =>
        (await _validator.TestValidateAsync(Command(next: "abcdefgh", confirm: "abcdefgh")))
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Mật khẩu phải có ít nhất một chữ cái và một chữ số.");

    [Fact(DisplayName = "UTCID11 (A) - Xác nhận không khớp → báo lỗi ConfirmNewPassword")]
    public async Task Validate_MismatchedConfirmation_HasError() =>
        (await _validator.TestValidateAsync(Command(confirm: "Different123")))
            .ShouldHaveValidationErrorFor(x => x.ConfirmNewPassword)
            .WithErrorMessage("Mật khẩu xác nhận không khớp.");

    [Fact(DisplayName = "UTCID12 (A) - Mật khẩu mới trùng mật khẩu cũ → báo lỗi NewPassword")]
    public async Task Validate_SameOldAndNewPassword_HasError() =>
        (await _validator.TestValidateAsync(Command(current: "NewPassword123")))
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");

    [Fact(DisplayName = "UTCID13 (A) - Mật khẩu mới null → báo lỗi input, không ném exception")]
    public async Task Validate_NullNewPassword_HasError() =>
        (await _validator.TestValidateAsync(Command(next: null, confirm: null)))
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Vui lòng nhập mật khẩu mới.");

    private static ChangePasswordCommand Command(
        string current = "Current123", string? next = "NewPassword123", string? confirm = "NewPassword123") =>
        new() { CurrentPassword = current, NewPassword = next!, ConfirmNewPassword = confirm! };
}

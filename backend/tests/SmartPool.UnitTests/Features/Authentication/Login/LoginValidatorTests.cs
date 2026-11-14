using FluentValidation.TestHelper;
using SmartPool.Application.Features.Authentication.Commands.Login;

namespace SmartPool.UnitTests.Features.Authentication.Login;

public sealed class LoginValidatorTests
{
    private readonly LoginValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (A) - Identifier rỗng → Báo lỗi 'Vui lòng nhập tên đăng nhập, email hoặc số điện thoại.'")]
    public async Task Validate_EmptyIdentifier_ShouldHaveValidationError()
    {
        var command = new LoginCommand { Identifier = "", Password = "Password123" };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Identifier)
            .WithErrorMessage("Vui lòng nhập tên đăng nhập, email hoặc số điện thoại.");
    }

    [Fact(DisplayName = "UTCID02 (B) - Identifier chỉ chứa khoảng trắng → Báo lỗi 'Vui lòng nhập tên đăng nhập, email hoặc số điện thoại.'")]
    public async Task Validate_WhitespaceIdentifier_ShouldHaveValidationError()
    {
        var command = new LoginCommand { Identifier = "   ", Password = "Password123" };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Identifier)
            .WithErrorMessage("Vui lòng nhập tên đăng nhập, email hoặc số điện thoại.");
    }

    [Fact(DisplayName = "UTCID03 (B) - Identifier vượt 150 ký tự (151 ký tự) → Báo lỗi 'Thông tin đăng nhập không được vượt quá 150 ký tự.'")]
    public async Task Validate_IdentifierExceeds150Chars_ShouldHaveValidationError()
    {
        var command = new LoginCommand { Identifier = new string('a', 151), Password = "Password123" };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Identifier)
            .WithErrorMessage("Thông tin đăng nhập không được vượt quá 150 ký tự.");
    }

    [Fact(DisplayName = "UTCID04 (A) - Mật khẩu rỗng → Báo lỗi 'Vui lòng nhập mật khẩu.'")]
    public async Task Validate_EmptyPassword_ShouldHaveValidationError()
    {
        var command = new LoginCommand { Identifier = "customer", Password = "" };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Vui lòng nhập mật khẩu.");
    }

    [Fact(DisplayName = "UTCID05 (B) - Identifier sau Trim đúng 150 ký tự → Không có lỗi validation")]
    public async Task Validate_ValidIdentifierExact150CharsAndPassword_ShouldNotHaveValidationError()
    {
        // Giới hạn áp dụng sau Trim(): 150 ký tự nội dung, thêm khoảng trắng ở hai đầu.
        var identifier = "  " + new string('a', 150) + "  ";
        var command = new LoginCommand { Identifier = identifier, Password = "Password123" };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

using SmartPool.Application.Features.Authentication.Commands.Logout;
using Xunit;

namespace SmartPool.UnitTests.Features.Authentication.Logout;

public sealed class LogoutValidatorTests
{
    private readonly LogoutValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (A) - RefreshToken rỗng → Trả lỗi 'Vui lòng cung cấp refresh token.'")]
    public void Validate_EmptyToken_ReturnsValidationError()
    {
        // Arrange
        var command = new LogoutCommand { RefreshToken = string.Empty };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("RefreshToken", error.PropertyName);
        Assert.Equal("Vui lòng cung cấp refresh token.", error.ErrorMessage);
    }

    [Fact(DisplayName = "UTCID02 (B) - RefreshToken dài 513 ký tự → Trả lỗi 'Refresh token không hợp lệ.'")]
    public void Validate_TokenExceeds512Chars_ReturnsValidationError()
    {
        // Arrange
        var command = new LogoutCommand { RefreshToken = new string('a', 513) };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("RefreshToken", error.PropertyName);
        Assert.Equal("Refresh token không hợp lệ.", error.ErrorMessage);
    }

    [Fact(DisplayName = "UTCID03 (B) - RefreshToken đúng 512 ký tự → Hợp lệ")]
    public void Validate_TokenExactly512Chars_IsValid()
    {
        // Arrange
        var command = new LogoutCommand { RefreshToken = new string('a', 512) };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
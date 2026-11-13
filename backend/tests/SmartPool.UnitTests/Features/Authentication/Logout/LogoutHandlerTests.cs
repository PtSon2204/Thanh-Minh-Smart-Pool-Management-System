using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.Authentication.Commands.Logout;
using SmartPool.Application.Interfaces.Services;
using Xunit;

namespace SmartPool.UnitTests.Features.Authentication.Logout;

public sealed class LogoutHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Refresh token hợp lệ → gọi RevokeAsync một lần, trả 204")]
    public async Task Handle_ValidToken_CallsRevokeAndReturnsNoContent()
    {
        // Arrange
        var harness = new Harness();
        var command = new LogoutCommand { RefreshToken = "valid-token" };

        // Act
        var result = await harness.Handle(command);

        // Assert
        Assert.IsType<NoContentResult>(result);
        harness.RefreshTokenService.Verify(
            s => s.RevokeAsync("valid-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Token rỗng → 400, không gọi revoke")]
    public async Task Handle_EmptyToken_ReturnsBadRequest_DoesNotCallRevoke()
    {
        // Arrange
        var harness = new Harness();
        var command = new LogoutCommand { RefreshToken = string.Empty };

        // Act
        var result = await harness.Handle(command);

        // Assert
        AssertValidationProblem(result, "refreshToken", "Vui lòng cung cấp refresh token.");
        harness.RefreshTokenService.Verify(
            s => s.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID03 (B) - Token quá 512 ký tự → 400, không gọi revoke")]
    public async Task Handle_TokenExceeds512Chars_ReturnsBadRequest_DoesNotCallRevoke()
    {
        // Arrange
        var harness = new Harness();
        var command = new LogoutCommand { RefreshToken = new string('a', 513) };

        // Act
        var result = await harness.Handle(command);

        // Assert
        AssertValidationProblem(result, "refreshToken", "Refresh token không hợp lệ.");
        harness.RefreshTokenService.Verify(
            s => s.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID04 (B) - Đăng xuất hai lần cùng token → Cả hai lần đều trả 204")]
    public async Task Handle_RepeatedLogout_ReturnsNoContentBothTimes()
    {
        // Arrange
        var harness = new Harness();
        var command = new LogoutCommand { RefreshToken = "valid-token" };

        // Act
        var firstResult = await harness.Handle(command);
        var secondResult = await harness.Handle(command);

        // Assert
        Assert.IsType<NoContentResult>(firstResult);
        Assert.IsType<NoContentResult>(secondResult);
        harness.RefreshTokenService.Verify(
            s => s.RevokeAsync("valid-token", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "UTCID05 (A) - RevokeAsync ném exception → handler truyền tiếp exception")]
    public async Task Handle_RevokeAsyncThrows_ThrowsException()
    {
        // Arrange
        var harness = new Harness();
        var command = new LogoutCommand { RefreshToken = "valid-token" };
        var failure = new InvalidOperationException("Redis Failure");
        harness.RefreshTokenService.Setup(s => s.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Handle(command));
        Assert.Same(failure, exception);
        harness.RefreshTokenService.Verify(
            s => s.RevokeAsync("valid-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Helper Methods ---

    private static void AssertValidationProblem(IActionResult result, string expectedProperty, string expectedErrorMessage)
    {
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var problem = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        Assert.True(problem.Errors.ContainsKey(expectedProperty), $"Validation errors must contain key '{expectedProperty}'.");
        Assert.Contains(expectedErrorMessage, problem.Errors[expectedProperty]);
    }

    private sealed class Harness
    {
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public LogoutValidator Validator { get; } = new(); // Dùng thẳng validator thật

        public Harness()
        {
            RefreshTokenService.Setup(service => service.RevokeAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public Task<IActionResult> Handle(LogoutCommand request)
        {
            var handler = new LogoutHandler(
                RefreshTokenService.Object,
                Validator);

            return handler.Handle(request, CancellationToken.None);
        }
    }
}

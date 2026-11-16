using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.Authentication.Commands.ChangePassword;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.Authentication.ChangePassword;

public sealed class ChangePasswordHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Đổi mật khẩu hợp lệ → 204 và cập nhật bằng hash mới")]
    public async Task Handle_ValidRequest_ChangesPasswordAndReturnsNoContent()
    {
        var harness = new Harness();

        var result = await harness.Handle();

        Assert.IsType<NoContentResult>(result);
        harness.PasswordHasher.Verify(x => x.VerifyPassword(harness.User, "old-hash", "Current123"), Times.Once);
        harness.PasswordHasher.Verify(x => x.HashPassword(harness.User, "NewPassword123"), Times.Once);
        harness.Users.Verify(x => x.TryChangePasswordAsync(harness.User.Id, "old-hash", "new-hash", harness.CancellationToken), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Input không hợp lệ → 400 với lỗi trường, không truy cập tài khoản")]
    public async Task Handle_InvalidInput_ReturnsValidationProblem()
    {
        var harness = new Harness();

        var result = await harness.Handle(new ChangePasswordCommand
        {
            CurrentPassword = "Current123", NewPassword = "short", ConfirmNewPassword = "short"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Contains("Mật khẩu phải dài từ 8 đến 128 ký tự.", problem.Errors[nameof(ChangePasswordCommand.NewPassword)]);
        harness.AssertNoUserOrPasswordCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiếu ID trong JWT → 401, không truy cập tài khoản")]
    public async Task Handle_MissingUserId_ReturnsUnauthorized()
    {
        var harness = new Harness(userIdClaim: null);

        Assert.IsType<UnauthorizedResult>(await harness.Handle());
        harness.AssertNoUserOrPasswordCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - ID trong JWT sai định dạng → 401")]
    public async Task Handle_InvalidUserId_ReturnsUnauthorized()
    {
        var harness = new Harness(userIdClaim: "invalid-guid");

        Assert.IsType<UnauthorizedResult>(await harness.Handle());
        harness.AssertNoUserOrPasswordCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Không tìm thấy tài khoản → 401, không hash hay cập nhật")]
    public async Task Handle_UserNotFound_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.Users.Setup(x => x.GetByIdAsync(harness.User.Id, harness.CancellationToken)).ReturnsAsync((User?)null);

        Assert.IsType<UnauthorizedResult>(await harness.Handle());
        harness.AssertNoPasswordCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Tài khoản không có password hash → 401")]
    public async Task Handle_UserWithoutPasswordHash_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.User.PasswordHash = " ";

        Assert.IsType<UnauthorizedResult>(await harness.Handle());
        harness.AssertNoPasswordCalls();
    }

    [Fact(DisplayName = "UTCID07 (A) - Sai mật khẩu hiện tại → 400, không cập nhật")]
    public async Task Handle_WrongCurrentPassword_ReturnsBadRequest()
    {
        var harness = new Harness();
        harness.PasswordHasher.Setup(x => x.VerifyPassword(harness.User, "old-hash", "Current123")).Returns(false);

        var result = await harness.Handle();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("Mật khẩu hiện tại không chính xác.",
            JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value)).RootElement.GetProperty("message").GetString());
        harness.PasswordHasher.Verify(x => x.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        harness.Users.Verify(x => x.TryChangePasswordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID08 (B) - Mật khẩu đã đổi bởi yêu cầu khác → 409")]
    public async Task Handle_ConcurrentPasswordChange_ReturnsConflict()
    {
        var harness = new Harness();
        harness.Users.Setup(x => x.TryChangePasswordAsync(harness.User.Id, "old-hash", "new-hash", harness.CancellationToken))
            .ReturnsAsync(false);

        Assert.IsType<ConflictResult>(await harness.Handle());
        harness.Users.Verify(x => x.TryChangePasswordAsync(harness.User.Id, "old-hash", "new-hash", harness.CancellationToken), Times.Once);
    }

    [Fact(DisplayName = "UTCID09 (A) - Lỗi đọc tài khoản → exception được truyền tiếp, không đổi mật khẩu")]
    public async Task Handle_UserLookupThrows_PropagatesException()
    {
        var harness = new Harness();
        var failure = new InvalidOperationException("Database unavailable");
        harness.Users.Setup(x => x.GetByIdAsync(harness.User.Id, harness.CancellationToken)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Handle()));
        harness.AssertNoPasswordCalls();
    }

    [Fact(DisplayName = "UTCID10 (A) - Lỗi cập nhật mật khẩu → exception được truyền tiếp")]
    public async Task Handle_PasswordUpdateThrows_PropagatesException()
    {
        var harness = new Harness();
        var failure = new InvalidOperationException("Database unavailable");
        harness.Users.Setup(x => x.TryChangePasswordAsync(harness.User.Id, "old-hash", "new-hash", harness.CancellationToken))
            .ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Handle()));
        harness.Users.Verify(x => x.TryChangePasswordAsync(harness.User.Id, "old-hash", "new-hash", harness.CancellationToken), Times.Once);
    }

    private sealed class Harness
    {
        public readonly Mock<IUserOperations> Users = new();
        public readonly Mock<IPasswordHasher> PasswordHasher = new();
        public readonly User User = new() { Id = Guid.NewGuid(), PasswordHash = "old-hash" };
        public readonly CancellationToken CancellationToken = new CancellationTokenSource().Token;
        private readonly ChangePasswordHandler _handler;

        public Harness(string? userIdClaim = "valid")
        {
            var identity = new ClaimsIdentity(authenticationType: "test");
            if (userIdClaim is not null)
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userIdClaim == "valid" ? User.Id.ToString() : userIdClaim));
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            _handler = new ChangePasswordHandler(Users.Object, PasswordHasher.Object,
                new ChangePasswordValidator(), new HttpContextAccessor { HttpContext = context });
            Users.Setup(x => x.GetByIdAsync(User.Id, CancellationToken)).ReturnsAsync(User);
            Users.Setup(x => x.TryChangePasswordAsync(User.Id, "old-hash", "new-hash", CancellationToken)).ReturnsAsync(true);
            PasswordHasher.Setup(x => x.VerifyPassword(User, "old-hash", "Current123")).Returns(true);
            PasswordHasher.Setup(x => x.HashPassword(User, "NewPassword123")).Returns("new-hash");
        }

        public Task<IActionResult> Handle(ChangePasswordCommand? command = null) => _handler.Handle(command ?? new ChangePasswordCommand
        {
            CurrentPassword = "Current123", NewPassword = "NewPassword123", ConfirmNewPassword = "NewPassword123"
        }, CancellationToken);

        public void AssertNoUserOrPasswordCalls()
        {
            Users.VerifyNoOtherCalls();
            PasswordHasher.VerifyNoOtherCalls();
        }

        public void AssertNoPasswordCalls()
        {
            PasswordHasher.VerifyNoOtherCalls();
            Users.Verify(x => x.TryChangePasswordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}

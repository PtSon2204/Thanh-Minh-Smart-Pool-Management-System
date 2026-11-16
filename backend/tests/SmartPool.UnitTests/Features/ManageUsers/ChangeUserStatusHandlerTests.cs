using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;

namespace SmartPool.UnitTests.Features.ManageUsers;

public sealed class ChangeUserStatusHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Admin khóa user → 200, chuyển LOCKED cùng actor ID")]
    public async Task Handle_LockUser_ReturnsOk()
    {
        var h = new Harness();

        var result = await h.Handle("LOCKED");

        var response = Assert.IsType<ChangeUserStatusResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(h.TargetId, response.Id);
        Assert.Equal("LOCKED", response.Status);
        Assert.NotNull(response.UpdatedAt);
        h.Users.Verify(x => x.ChangeUserStatusAsync(h.TargetId, h.ActorId, UserStatusEnum.LOCKED, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Admin mở khóa user → 200, chuyển ACTIVE")]
    public async Task Handle_UnlockUser_ReturnsOk()
    {
        var h = new Harness();

        var result = await h.Handle("ACTIVE");

        var response = Assert.IsType<ChangeUserStatusResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("ACTIVE", response.Status);
        h.Users.Verify(x => x.ChangeUserStatusAsync(h.TargetId, h.ActorId, UserStatusEnum.ACTIVE, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (B) - Status có chữ thường và khoảng trắng → chuẩn hóa trước khi đổi")]
    public async Task Handle_LowercaseStatusWithSpaces_NormalizesEnum()
    {
        var h = new Harness();

        Assert.IsType<OkObjectResult>(await h.Handle(" locked "));

        h.Users.Verify(x => x.ChangeUserStatusAsync(h.TargetId, h.ActorId, UserStatusEnum.LOCKED, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID04 (A) - Id rỗng → 400, không gọi repository")]
    public async Task Handle_EmptyId_ReturnsValidationProblem()
    {
        var h = new Harness();

        var result = await h.Handle("LOCKED", Guid.Empty);

        AssertValidationError(result, "id", "Mã người dùng không hợp lệ.");
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Status INACTIVE → 400, không gọi repository")]
    public async Task Handle_UnsupportedStatus_ReturnsValidationProblem()
    {
        var h = new Harness();

        var result = await h.Handle("INACTIVE");

        AssertValidationError(result, "status", "Trạng thái phải là ACTIVE hoặc LOCKED.");
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Thiếu actor ID → 403, không gọi repository")]
    public async Task Handle_NoActorId_ReturnsForbid()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        Assert.IsType<ForbidResult>(await h.Handle("LOCKED"));
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID07 (A) - Actor không có role ADMIN → 403, không gọi repository")]
    public async Task Handle_NonAdminActor_ReturnsForbid()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("ADMIN")).Returns(false);

        Assert.IsType<ForbidResult>(await h.Handle("LOCKED"));
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID08 (A) - User không tồn tại hoặc đã xóa → 404")]
    public async Task Handle_TargetUnavailable_ReturnsNotFound()
    {
        var h = new Harness();
        h.Users.Setup(x => x.ChangeUserStatusAsync(h.TargetId, h.ActorId, UserStatusEnum.LOCKED, h.Token))
            .ReturnsAsync((ChangeUserStatusResponse?)null);

        var result = await h.Handle("LOCKED");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<NotFoundObjectResult>(result).Value);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Không tìm thấy người dùng.", problem.Title);
    }

    [Fact(DisplayName = "UTCID09 (A) - Repository từ chối khóa chính mình → 403")]
    public async Task Handle_RepositoryForbids_ReturnsForbid()
    {
        var h = new Harness();
        h.Users.Setup(x => x.ChangeUserStatusAsync(h.TargetId, h.ActorId, UserStatusEnum.LOCKED, h.Token))
            .ThrowsAsync(new StatusChangeForbiddenException());

        Assert.IsType<ForbidResult>(await h.Handle("LOCKED"));
        h.Users.Verify(x => x.ChangeUserStatusAsync(h.TargetId, h.ActorId, UserStatusEnum.LOCKED, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID10 (A) - Không thể khóa Admin cuối cùng → 409 với lý do")]
    public async Task Handle_StatusConflict_ReturnsConflict()
    {
        var h = new Harness();
        const string reason = "Hệ thống phải còn ít nhất một Admin đang hoạt động.";
        h.Users.Setup(x => x.ChangeUserStatusAsync(h.TargetId, h.ActorId, UserStatusEnum.LOCKED, h.Token))
            .ThrowsAsync(new StatusChangeConflictException(reason));

        var result = await h.Handle("LOCKED");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ConflictObjectResult>(result).Value);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Không thể thay đổi trạng thái tài khoản.", problem.Title);
        Assert.Equal(reason, problem.Detail);
    }

    [Fact(DisplayName = "UTCID11 (A) - Repository lỗi bất ngờ → exception được truyền tiếp")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Users.Setup(x => x.ChangeUserStatusAsync(h.TargetId, h.ActorId, UserStatusEnum.LOCKED, h.Token))
            .ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle("LOCKED")));
        h.Users.Verify(x => x.ChangeUserStatusAsync(h.TargetId, h.ActorId, UserStatusEnum.LOCKED, h.Token), Times.Once);
    }

    private static void AssertValidationError(IActionResult result, string field, string message)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains(message, problem.Errors[field]);
    }

    private sealed class Harness
    {
        public readonly Guid TargetId = Guid.NewGuid();
        public readonly Guid ActorId = Guid.NewGuid();
        public readonly Mock<IUserOperations> Users = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        private readonly ChangeUserStatusHandler _handler;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(ActorId);
            CurrentUser.Setup(x => x.IsInRole("ADMIN")).Returns(true);
            Users.Setup(x => x.ChangeUserStatusAsync(TargetId, ActorId, It.IsAny<UserStatusEnum>(), Token))
                .ReturnsAsync((Guid _, Guid _, UserStatusEnum status, CancellationToken _) =>
                    new ChangeUserStatusResponse { Id = TargetId, Status = status.ToString(), UpdatedAt = DateTime.UtcNow });
            _handler = new ChangeUserStatusHandler(Users.Object, CurrentUser.Object, new ChangeUserStatusValidator());
        }

        public Task<IActionResult> Handle(string status, Guid? id = null) =>
            _handler.Handle(new ChangeUserStatusCommand { Id = id ?? TargetId, Status = status }, Token);
    }
}

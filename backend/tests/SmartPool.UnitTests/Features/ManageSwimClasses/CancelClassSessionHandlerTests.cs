using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassSession;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CancelClassSessionHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Coach hủy tiết hợp lệ → trả Cancelled và gọi repository đúng actor")]
    public async Task Handle_ValidCommand_ReturnsCancelled()
    {
        var h = new Harness();
        var request = h.Request();
        var expected = h.Response();
        h.Operations.Setup(x => x.CancelSessionAsync(request, h.CoachId, h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle(request);

        Assert.Same(expected, actual);
        Assert.Equal(h.SessionId, actual.Id);
        Assert.Equal("Cancelled", actual.Status);
        Assert.Equal("Coach nghỉ đột xuất", actual.CancelReason);
        h.Operations.Verify(x => x.CancelSessionAsync(request, h.CoachId, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Thiếu Coach ID → từ chối trước khi gọi repository")]
    public async Task Handle_MissingUserId_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        Assert.Equal("Chỉ Coach được hủy buổi học.", error.Message);
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - User không có role COACH → từ chối trước khi gọi repository")]
    public async Task Handle_NonCoach_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Lý do trống → lỗi validation và không hủy tiết")]
    public async Task Handle_EmptyReason_Rejects()
    {
        var h = new Harness();
        var request = h.Request();
        request.Reason = " ";

        var error = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(request));

        Assert.Contains(error.Errors, x => x.PropertyName == "Reason");
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Tiết không thuộc lớp → truyền lỗi không tìm thấy")]
    public async Task Handle_MissingSession_PropagatesNotFound()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.CancelSessionAsync(It.IsAny<CancelClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy buổi học của lớp."));

        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle());

        Assert.Equal("Không tìm thấy buổi học của lớp.", error.Message);
    }

    [Fact(DisplayName = "UTCID06 (A) - Coach không sở hữu lớp → truyền lỗi quyền truy cập")]
    public async Task Handle_NotOwner_PropagatesUnauthorized()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.CancelSessionAsync(It.IsAny<CancelClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new UnauthorizedAccessException("Chỉ Coach tạo lớp được quản lý lớp này."));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID07 (A) - Phiên bản tiết đã cũ → truyền xung đột trạng thái")]
    public async Task Handle_StaleVersion_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.CancelSessionAsync(It.IsAny<CancelClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Buổi học đã được cập nhật. Vui lòng tải lại trước khi hủy."));

        var error = await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());

        Assert.Contains("tải lại", error.Message);
    }

    [Fact(DisplayName = "UTCID08 (A) - Tiết đã bắt đầu hoặc đã hủy → truyền lỗi trạng thái")]
    public async Task Handle_UncancellableSession_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.CancelSessionAsync(It.IsAny<CancelClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Chỉ có thể hủy buổi Scheduled chưa bắt đầu của lớp Draft hoặc Active."));

        await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID09 (A) - Lưu dữ liệu lỗi → truyền tiếp exception")]
    public async Task Handle_RepositoryFailure_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Operations.Setup(x => x.CancelSessionAsync(It.IsAny<CancelClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
    }

    private sealed class Harness
    {
        public readonly Guid CoachId = Guid.NewGuid();
        public readonly Guid ClassId = Guid.NewGuid();
        public readonly Guid SessionId = Guid.NewGuid();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly Mock<ISwimClassOperations> Operations = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        private readonly CancelClassSessionHandler _handler;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CoachId);
            CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(true);
            _handler = new CancelClassSessionHandler(Operations.Object, CurrentUser.Object,
                new CancelClassSessionValidator());
        }

        public CancelClassSessionCommand Request() => new()
        {
            ClassId = ClassId,
            SessionId = SessionId,
            Reason = "Coach nghỉ đột xuất",
            ExpectedUpdatedAt = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero)
        };

        public CancelClassSessionResponse Response() =>
            new(SessionId, ClassId, "Cancelled", "Coach nghỉ đột xuất", DateTime.UtcNow);

        public Task<CancelClassSessionResponse> Handle(CancelClassSessionCommand? request = null) =>
            _handler.Handle(request ?? Request(), Token);
    }
}

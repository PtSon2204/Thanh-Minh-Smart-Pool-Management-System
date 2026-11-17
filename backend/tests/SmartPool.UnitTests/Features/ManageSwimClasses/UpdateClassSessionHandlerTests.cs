using FluentValidation;
using FluentValidation.Results;
using Moq;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class UpdateClassSessionHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Coach sửa buổi học hợp lệ → trả lịch mới và gọi repository đúng actor")]
    public async Task Handle_ValidRequest_ReturnsUpdatedSession()
    {
        var h = new Harness();
        var request = h.Request();
        var expected = h.Response();
        h.Operations.Setup(x => x.UpdateSessionAsync(request, h.CoachId, h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle(request);

        Assert.Same(expected, actual);
        Assert.Equal(request.SessionId, actual.Id);
        Assert.Equal("Scheduled", actual.Status);
        h.Operations.Verify(x => x.UpdateSessionAsync(request, h.CoachId, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Thiếu Coach ID → từ chối và không cập nhật")]
    public async Task Handle_MissingUserId_RejectsBeforeRepository()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        Assert.Equal("Chỉ Coach được cập nhật buổi học.", error.Message);
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - User không có role COACH → từ chối và không cập nhật")]
    public async Task Handle_NonCoach_RejectsBeforeRepository()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Thiếu phiên bản cập nhật gần nhất → lỗi validation, không gọi repository")]
    public async Task Handle_MissingVersion_RejectsBeforeRepository()
    {
        var h = new Harness();
        var request = h.Request();
        request.ExpectedUpdatedAt = default;

        var error = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(request));

        Assert.Contains(error.Errors, x => x.PropertyName == "ExpectedUpdatedAt" &&
            x.ErrorMessage == "Cần thời gian cập nhật gần nhất của buổi học.");
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Buổi học không thuộc lớp → truyền lỗi không tìm thấy")]
    public async Task Handle_SessionMissing_PropagatesNotFound()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.UpdateSessionAsync(It.IsAny<UpdateClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy buổi học của lớp."));

        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle());

        Assert.Equal("Không tìm thấy buổi học của lớp.", error.Message);
    }

    [Fact(DisplayName = "UTCID06 (A) - Coach không sở hữu lớp → truyền lỗi quyền truy cập")]
    public async Task Handle_NotOwner_PropagatesUnauthorized()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.UpdateSessionAsync(It.IsAny<UpdateClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new UnauthorizedAccessException("Chỉ Coach tạo lớp được quản lý lớp này."));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID07 (A) - Phiên bản đã cũ → truyền lỗi xung đột trạng thái")]
    public async Task Handle_StaleVersion_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.UpdateSessionAsync(It.IsAny<UpdateClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Buổi học đã được người khác cập nhật. Vui lòng tải lại trước khi lưu."));

        var error = await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());

        Assert.Contains("tải lại", error.Message);
    }

    [Fact(DisplayName = "UTCID08 (A) - Buổi đã diễn ra hoặc không Scheduled → truyền lỗi trạng thái")]
    public async Task Handle_UneditableSession_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.UpdateSessionAsync(It.IsAny<UpdateClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Chỉ có thể sửa buổi đã lên lịch và chưa diễn ra của lớp đang hoạt động hoặc bản nháp."));

        await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID09 (A) - Lịch mới trùng lịch lớp hoặc Coach → trả danh sách xung đột")]
    public async Task Handle_OverlappingSchedule_PropagatesConflicts()
    {
        var h = new Harness();
        var conflict = new ClassSessionConflict(0, h.CoachId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "coach_overlap");
        h.Operations.Setup(x => x.UpdateSessionAsync(It.IsAny<UpdateClassSessionCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new ClassSessionConflictException("Buổi học bị trùng lịch lớp hoặc lịch Coach.", [conflict]));

        var error = await Assert.ThrowsAsync<ClassSessionConflictException>(() => h.Handle());

        Assert.Same(conflict, Assert.Single(error.Conflicts));
    }

    [Fact(DisplayName = "UTCID10 (A) - Lỗi lưu dữ liệu → truyền tiếp exception")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Operations.Setup(x => x.UpdateSessionAsync(It.IsAny<UpdateClassSessionCommand>(), h.CoachId, h.Token))
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
        private readonly UpdateClassSessionHandler _handler;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CoachId);
            CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(true);
            _handler = new UpdateClassSessionHandler(Operations.Object, CurrentUser.Object,
                new UpdateClassSessionValidator(new FixedClock()));
        }

        public UpdateClassSessionCommand Request() => new()
        {
            ClassId = ClassId,
            SessionId = SessionId,
            ExpectedUpdatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
            StartsAt = new DateTimeOffset(2026, 11, 20, 6, 0, 0, TimeSpan.FromHours(7)),
            EndsAt = new DateTimeOffset(2026, 11, 20, 7, 0, 0, TimeSpan.FromHours(7))
        };

        public UpdateClassSessionResponse Response()
        {
            var now = DateTime.UtcNow;
            return new(SessionId, ClassId, CoachId, now.AddDays(4), now.AddDays(4).AddHours(1),
                "Scheduled", now.AddDays(-1), now);
        }

        public Task<UpdateClassSessionResponse> Handle(UpdateClassSessionCommand? request = null) =>
            _handler.Handle(request ?? Request(), Token);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
    }
}

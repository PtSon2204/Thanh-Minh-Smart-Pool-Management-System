using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ReviewClassAttendanceHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Coach xác nhận điểm danh → trả Verified và gọi repository đúng actor")]
    public async Task Handle_Verified_ReturnsVerified()
    {
        var h = new Harness();
        var request = h.Request();
        var expected = h.Response("Verified");
        h.Operations.Setup(x => x.ReviewClassAttendanceAsync(request, h.CoachId, h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle(request);

        Assert.Same(expected, actual);
        Assert.Equal("Verified", actual.Status);
        h.Operations.Verify(x => x.ReviewClassAttendanceAsync(request, h.CoachId, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Coach từ chối kèm lý do → trả Rejected")]
    public async Task Handle_Rejected_ReturnsRejected()
    {
        var h = new Harness();
        var request = h.Request("Rejected", "Ảnh không rõ");
        var expected = h.Response("Rejected", "Ảnh không rõ");
        h.Operations.Setup(x => x.ReviewClassAttendanceAsync(request, h.CoachId, h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle(request);

        Assert.Equal("Rejected", actual.Status);
        Assert.Equal("Ảnh không rõ", actual.ReviewNote);
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiếu Coach ID → từ chối và không gọi repository")]
    public async Task Handle_MissingUserId_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - User không có role COACH → từ chối")]
    public async Task Handle_NonCoach_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Admin đồng thời có role COACH → từ chối")]
    public async Task Handle_AdminCoach_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("ADMIN")).Returns(true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Rejected thiếu lý do → lỗi validation, không gọi repository")]
    public async Task Handle_RejectWithoutReason_Rejects()
    {
        var h = new Harness();

        var error = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(h.Request("Rejected")));

        Assert.Contains(error.Errors, x => x.PropertyName == "ReviewNote");
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID07 (A) - Không tìm thấy buổi/điểm danh → truyền lỗi 404")]
    public async Task Handle_MissingAttendance_PropagatesNotFound()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.ReviewClassAttendanceAsync(It.IsAny<ReviewClassAttendanceCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy điểm danh của buổi học."));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID08 (A) - Coach không phụ trách buổi → truyền lỗi quyền")]
    public async Task Handle_NotAssigned_PropagatesUnauthorized()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.ReviewClassAttendanceAsync(It.IsAny<ReviewClassAttendanceCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được duyệt điểm danh."));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID09 (A) - Điểm danh đã được review → truyền xung đột trạng thái")]
    public async Task Handle_AlreadyReviewed_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.ReviewClassAttendanceAsync(It.IsAny<ReviewClassAttendanceCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Chỉ có thể duyệt điểm danh đang chờ xem xét."));

        await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID10 (A) - Repository lỗi → truyền tiếp exception")]
    public async Task Handle_RepositoryFailure_Propagates()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Operations.Setup(x => x.ReviewClassAttendanceAsync(It.IsAny<ReviewClassAttendanceCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
    }

    private sealed class Harness
    {
        public readonly Guid CoachId = Guid.NewGuid();
        public readonly Guid ClassId = Guid.NewGuid();
        public readonly Guid SessionId = Guid.NewGuid();
        public readonly Guid AttendanceId = Guid.NewGuid();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly Mock<ISwimClassOperations> Operations = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        private readonly ReviewClassAttendanceHandler _handler;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CoachId);
            CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(true);
            _handler = new ReviewClassAttendanceHandler(Operations.Object, CurrentUser.Object,
                new ReviewClassAttendanceValidator());
        }

        public ReviewClassAttendanceCommand Request(string decision = "Verified", string? note = null) => new()
        {
            ClassId = ClassId, SessionId = SessionId, AttendanceId = AttendanceId,
            Decision = decision, ReviewNote = note,
            ExpectedUpdatedAt = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero)
        };

        public ReviewClassAttendanceResponse Response(string status, string? note = null)
        {
            var now = DateTime.UtcNow;
            return new(AttendanceId, ClassId, SessionId, status, CoachId, now, note, now);
        }

        public Task<ReviewClassAttendanceResponse> Handle(ReviewClassAttendanceCommand? request = null) =>
            _handler.Handle(request ?? Request(), Token);
    }
}

using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ReviewClassRegistrationHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Coach chấp nhận yêu cầu → trả Confirmed và gọi repository đúng actor")]
    public async Task Handle_Accepted_ReturnsConfirmed()
    {
        var h = new Harness();
        var request = h.Request();
        var expected = h.Response("Confirmed");
        h.Operations.Setup(x => x.ReviewClassRegistrationAsync(request, h.CoachId, h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle(request);

        Assert.Same(expected, actual);
        Assert.Equal("Confirmed", actual.Status);
        Assert.NotNull(actual.ConfirmedAt);
        h.Operations.Verify(x => x.ReviewClassRegistrationAsync(request, h.CoachId, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Coach từ chối kèm lý do → trả Rejected")]
    public async Task Handle_Rejected_ReturnsRejected()
    {
        var h = new Harness();
        var request = h.Request("Rejected", "Hồ sơ chưa phù hợp");
        var expected = h.Response("Rejected", "Hồ sơ chưa phù hợp");
        h.Operations.Setup(x => x.ReviewClassRegistrationAsync(request, h.CoachId, h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle(request);

        Assert.Equal("Rejected", actual.Status);
        Assert.Null(actual.ConfirmedAt);
        Assert.Equal("Hồ sơ chưa phù hợp", actual.ReviewNote);
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiếu Coach ID → từ chối và không gọi repository")]
    public async Task Handle_MissingUserId_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - User không có role COACH → từ chối và không gọi repository")]
    public async Task Handle_NonCoach_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Admin đồng thời có role COACH → từ chối review")]
    public async Task Handle_AdminCoach_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("ADMIN")).Returns(true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Từ chối không có lý do → lỗi validation, không gọi repository")]
    public async Task Handle_RejectWithoutReason_Rejects()
    {
        var h = new Harness();

        var error = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(h.Request("Rejected")));

        Assert.Contains(error.Errors, x => x.PropertyName == "ReviewNote");
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID07 (A) - Lớp không tồn tại → truyền lỗi không tìm thấy")]
    public async Task Handle_ClassMissing_PropagatesNotFound()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.ReviewClassRegistrationAsync(It.IsAny<ReviewClassRegistrationCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy lớp dạy bơi."));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID08 (A) - Coach không sở hữu lớp → truyền lỗi quyền")]
    public async Task Handle_NotOwner_PropagatesUnauthorized()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.ReviewClassRegistrationAsync(It.IsAny<ReviewClassRegistrationCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new UnauthorizedAccessException("Chỉ Coach tạo lớp được quản lý lớp này."));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID09 (A) - Phiên bản yêu cầu đã cũ → truyền xung đột trạng thái")]
    public async Task Handle_StaleVersion_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.ReviewClassRegistrationAsync(It.IsAny<ReviewClassRegistrationCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Đăng ký đã được cập nhật. Vui lòng tải lại trước khi duyệt."));

        await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID10 (A) - Lớp hết chỗ → truyền xung đột, không xác nhận")]
    public async Task Handle_FullClass_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.ReviewClassRegistrationAsync(It.IsAny<ReviewClassRegistrationCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Lớp đã hết chỗ; yêu cầu vẫn đang chờ duyệt."));

        await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID11 (A) - Repository lỗi → truyền tiếp exception")]
    public async Task Handle_RepositoryFailure_Propagates()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Operations.Setup(x => x.ReviewClassRegistrationAsync(It.IsAny<ReviewClassRegistrationCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
    }

    private sealed class Harness
    {
        public readonly Guid CoachId = Guid.NewGuid();
        public readonly Guid ClassId = Guid.NewGuid();
        public readonly Guid RegistrationId = Guid.NewGuid();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly Mock<ISwimClassOperations> Operations = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        private readonly ReviewClassRegistrationHandler _handler;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CoachId);
            CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(true);
            _handler = new ReviewClassRegistrationHandler(Operations.Object, CurrentUser.Object,
                new ReviewClassRegistrationValidator());
        }

        public ReviewClassRegistrationCommand Request(string decision = "Accepted", string? note = null) => new()
        {
            ClassId = ClassId,
            RegistrationId = RegistrationId,
            Decision = decision,
            ReviewNote = note,
            ExpectedUpdatedAt = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero)
        };

        public ReviewClassRegistrationResponse Response(string status, string? note = null)
        {
            var now = DateTime.UtcNow;
            return new(RegistrationId, ClassId, status, CoachId, now, note,
                status == "Confirmed" ? now : null, now);
        }

        public Task<ReviewClassRegistrationResponse> Handle(ReviewClassRegistrationCommand? request = null) =>
            _handler.Handle(request ?? Request(), Token);
    }
}

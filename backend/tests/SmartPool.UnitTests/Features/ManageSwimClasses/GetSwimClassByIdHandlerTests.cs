using Moq;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class GetSwimClassByIdHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Customer xem chi tiết → trả lớp và danh sách buổi học")]
    public async Task Handle_Customer_ReturnsDetail()
    {
        var h = new Harness();
        var expected = Detail(h.ClassId);
        h.Operations.Setup(x => x.GetByIdAsync(h.ClassId, h.UserId, "CUSTOMER", h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle();

        Assert.Same(expected, actual);
        Assert.Equal("Lớp bơi", actual.Name);
        Assert.Equal(1, actual.ConfirmedStudentCount);
        Assert.Single(actual.Sessions);
        h.Operations.Verify(x => x.GetByIdAsync(h.ClassId, h.UserId, "CUSTOMER", h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - User có quyền Coach chọn scope COACH → chuyển đúng role")]
    public async Task Handle_DualRoleWithCoachScope_UsesCoachScope()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(true);
        h.Operations.Setup(x => x.GetByIdAsync(h.ClassId, h.UserId, "COACH", h.Token)).ReturnsAsync(Detail(h.ClassId));

        await h.Handle(" coach ");

        h.Operations.Verify(x => x.GetByIdAsync(h.ClassId, h.UserId, "COACH", h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (A) - Scope không có quyền → từ chối, không đọc DB")]
    public async Task Handle_UnauthorizedScope_ThrowsBeforeRepository()
    {
        var h = new Harness();

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle("COACH"));

        Assert.Equal("Không có quyền xem phạm vi lớp được yêu cầu.", error.Message);
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Thiếu user ID → từ chối, không đọc DB")]
    public async Task Handle_MissingUserId_ThrowsBeforeRepository()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        Assert.Equal("Không có quyền xem lớp dạy bơi.", error.Message);
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Role không phải Customer/Coach → từ chối")]
    public async Task Handle_UnsupportedRole_ThrowsBeforeRepository()
    {
        var h = new Harness(role: "ADMIN");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Không tìm thấy lớp được phép xem → KeyNotFoundException")]
    public async Task Handle_ClassUnavailable_ThrowsNotFound()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.GetByIdAsync(h.ClassId, h.UserId, "CUSTOMER", h.Token))
            .ReturnsAsync((SwimClassDetailResponse?)null);

        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle());

        Assert.Equal("Không tìm thấy lớp dạy bơi.", error.Message);
    }

    [Fact(DisplayName = "UTCID07 (A) - Repository lỗi → exception được truyền tiếp")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Operations.Setup(x => x.GetByIdAsync(h.ClassId, h.UserId, "CUSTOMER", h.Token)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
    }

    [Fact(DisplayName = "UTCID08 (B) - Scope chỉ có khoảng trắng → dùng role hiện tại")]
    public async Task Handle_BlankScope_UsesCurrentRole()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.GetByIdAsync(h.ClassId, h.UserId, "CUSTOMER", h.Token)).ReturnsAsync(Detail(h.ClassId));

        await h.Handle("   ");

        h.Operations.Verify(x => x.GetByIdAsync(h.ClassId, h.UserId, "CUSTOMER", h.Token), Times.Once);
    }

    private static SwimClassDetailResponse Detail(Guid id)
    {
        var now = DateTime.UtcNow;
        var session = new SwimClassSessionDetail(Guid.NewGuid(), Guid.NewGuid(), "Coach", now.AddDays(1),
            now.AddDays(1).AddHours(1), "Scheduled", null, now);
        return new SwimClassDetailResponse(id, "Lớp bơi", "Cơ bản", 5, 100000m, session.CoachUserId,
            "Coach", now, now.AddDays(5), "Active", Guid.NewGuid(), 1, now, now, [session]);
    }

    private sealed class Harness
    {
        public readonly Guid ClassId = Guid.NewGuid();
        public readonly Guid UserId = Guid.NewGuid();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly Mock<ISwimClassOperations> Operations = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        private readonly GetSwimClassByIdHandler _handler;

        public Harness(string role = "CUSTOMER")
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(UserId);
            CurrentUser.SetupGet(x => x.Role).Returns(role);
            _handler = new GetSwimClassByIdHandler(Operations.Object, CurrentUser.Object);
        }

        public Task<SwimClassDetailResponse> Handle(string? scope = null) =>
            _handler.Handle(new GetSwimClassByIdQuery(ClassId, scope), Token);
    }
}

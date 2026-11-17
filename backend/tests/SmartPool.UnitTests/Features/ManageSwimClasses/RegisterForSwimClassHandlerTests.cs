using Moq;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class RegisterForSwimClassHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Customer gửi yêu cầu hợp lệ → trả PendingApproval và gọi repository đúng actor")]
    public async Task Handle_ValidRequest_ReturnsPending()
    {
        var h = new Harness();
        var expected = new RegisterForSwimClassResponse(Guid.NewGuid(), h.ClassId,
            "PendingApproval", DateTime.UtcNow, null);
        h.Operations.Setup(x => x.RegisterAsync(h.ClassId, h.CustomerId, h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle();

        Assert.Same(expected, actual);
        Assert.Null(actual.ConfirmedAt);
        h.Operations.Verify(x => x.RegisterAsync(h.ClassId, h.CustomerId, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (B) - Role CUSTOMER dạng chữ thường → vẫn được gửi yêu cầu")]
    public async Task Handle_CaseInsensitiveRole_AllowsRequest()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("CUSTOMER")).Returns(false);
        h.CurrentUser.SetupGet(x => x.Role).Returns("customer");
        h.Operations.Setup(x => x.RegisterAsync(h.ClassId, h.CustomerId, h.Token))
            .ReturnsAsync(new RegisterForSwimClassResponse(Guid.NewGuid(), h.ClassId,
                "PendingApproval", DateTime.UtcNow, null));

        await h.Handle();

        h.Operations.Verify(x => x.RegisterAsync(h.ClassId, h.CustomerId, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiếu Customer ID → từ chối, không tạo yêu cầu")]
    public async Task Handle_MissingUserId_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - User không có role CUSTOMER → từ chối, không tạo yêu cầu")]
    public async Task Handle_NonCustomer_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("CUSTOMER")).Returns(false);
        h.CurrentUser.SetupGet(x => x.Role).Returns("COACH");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - ClassId rỗng → lỗi input, không gọi repository")]
    public async Task Handle_EmptyClassId_Rejects()
    {
        var h = new Harness();

        await Assert.ThrowsAsync<ArgumentException>(() => h.Handle(Guid.Empty));

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Lớp không tồn tại → truyền lỗi không tìm thấy")]
    public async Task Handle_ClassMissing_PropagatesNotFound()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.RegisterAsync(h.ClassId, h.CustomerId, h.Token))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy lớp dạy bơi."));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID07 (A) - Lớp ngoài thời gian nhận đăng ký → truyền xung đột trạng thái")]
    public async Task Handle_OutsideRegistrationWindow_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.RegisterAsync(h.ClassId, h.CustomerId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Lớp không trong thời gian nhận đăng ký."));

        await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID08 (A) - Lớp hết chỗ → truyền xung đột trạng thái")]
    public async Task Handle_FullClass_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.RegisterAsync(h.ClassId, h.CustomerId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Lớp đã hết chỗ đăng ký."));

        await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID09 (A) - Repository lỗi → truyền tiếp exception")]
    public async Task Handle_RepositoryFailure_Propagates()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Operations.Setup(x => x.RegisterAsync(h.ClassId, h.CustomerId, h.Token)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
    }

    private sealed class Harness
    {
        public readonly Guid CustomerId = Guid.NewGuid();
        public readonly Guid ClassId = Guid.NewGuid();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly Mock<ISwimClassOperations> Operations = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        private readonly RegisterForSwimClassHandler _handler;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CustomerId);
            CurrentUser.Setup(x => x.IsInRole("CUSTOMER")).Returns(true);
            _handler = new RegisterForSwimClassHandler(Operations.Object, CurrentUser.Object);
        }

        public Task<RegisterForSwimClassResponse> Handle(Guid? classId = null) =>
            _handler.Handle(new RegisterForSwimClassCommand(classId ?? ClassId), Token);
    }
}

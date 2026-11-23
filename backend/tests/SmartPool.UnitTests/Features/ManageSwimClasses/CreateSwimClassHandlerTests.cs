using FluentValidation;
using FluentValidation.Results;
using Moq;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CreateSwimClassHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Coach tạo lớp hợp lệ → trả lớp và gọi CreateAsync đúng actor")]
    public async Task Handle_ValidClass_ReturnsCreatedClass()
    {
        var h = new Harness();
        var request = Request();
        var expected = Response(h.CoachId, request);
        h.Operations.Setup(x => x.CreateAsync(request, h.CoachId, h.Token)).ReturnsAsync(expected);

        var result = await h.Handle(request);

        Assert.Same(expected, result);
        Assert.Equal(h.CoachId, result.CoachUserId);
        Assert.Equal("Draft", result.Status);
        h.Operations.Verify(x => x.CreateAsync(request, h.CoachId, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Thiếu Coach ID → từ chối, không tạo lớp")]
    public async Task Handle_MissingUserId_RejectsBeforeRepository()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        Assert.Equal("Cần đăng nhập để tạo lớp dạy bơi.", error.Message);
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (N) - Không có claim COACH → repository vẫn xác minh IsCoach")]
    public async Task Handle_WithoutCoachRole_UsesRepositoryEligibility()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(false);
        var request = Request();
        var expected = Response(h.CoachId, request);
        h.Operations.Setup(x => x.CreateAsync(request, h.CoachId, h.Token)).ReturnsAsync(expected);

        Assert.Same(expected, await h.Handle(request));

        h.Operations.Verify(x => x.CreateAsync(request, h.CoachId, h.Token), Times.Once);
        h.CurrentUser.Verify(x => x.IsInRole("COACH"), Times.Never);
    }

    [Fact(DisplayName = "UTCID04 (A) - Sức chứa sai → ValidationException, không tạo lớp")]
    public async Task Handle_InvalidCapacity_RejectsBeforeRepository()
    {
        var h = new Harness();

        var error = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(Request(capacity: 6)));

        Assert.Contains(error.Errors, x => x.PropertyName == "Capacity" && x.ErrorMessage == "Sức chứa phải từ 1 đến 5 học viên.");
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Repository báo Coach không đủ điều kiện → truyền ValidationException")]
    public async Task Handle_RepositoryRejectsCoach_PropagatesValidationException()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.CreateAsync(It.IsAny<CreateSwimClassCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new ValidationException([new ValidationFailure("CreatedByUserId", "Người tạo lớp phải có tài khoản hoạt động và là Coach.")]));

        var error = await Assert.ThrowsAsync<ValidationException>(() => h.Handle());

        Assert.Contains(error.Errors, x => x.PropertyName == "CreatedByUserId");
    }

    [Fact(DisplayName = "UTCID06 (A) - Repository lỗi bất ngờ → truyền tiếp exception")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Operations.Setup(x => x.CreateAsync(It.IsAny<CreateSwimClassCommand>(), h.CoachId, h.Token)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
    }

    private static CreateSwimClassCommand Request(int capacity = 5) => new()
    {
        Name = "Lớp bơi cơ bản", Capacity = capacity, Fee = 100000m,
        RegistrationStartAt = new DateTimeOffset(2026, 11, 20, 6, 0, 0, TimeSpan.FromHours(7)),
        RegistrationEndAt = new DateTimeOffset(2026, 11, 21, 18, 0, 0, TimeSpan.FromHours(7))
    };

    private static CreateSwimClassResponse Response(Guid coachId, CreateSwimClassCommand request) =>
        new(Guid.NewGuid(), request.Name, request.Description, request.Capacity, request.Fee, coachId,
            request.RegistrationStartAt.UtcDateTime, request.RegistrationEndAt.UtcDateTime,
            "Draft", coachId, DateTime.UtcNow, DateTime.UtcNow);

    private sealed class Harness
    {
        public readonly Guid CoachId = Guid.NewGuid();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly Mock<ISwimClassOperations> Operations = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        private readonly CreateSwimClassHandler _handler;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CoachId);
            _handler = new CreateSwimClassHandler(Operations.Object, CurrentUser.Object, new CreateSwimClassValidator());
        }

        public Task<CreateSwimClassResponse> Handle(CreateSwimClassCommand? request = null) =>
            _handler.Handle(request ?? Request(), Token);
    }
}

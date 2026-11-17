using FluentValidation;
using FluentValidation.Results;
using Moq;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CreateClassSessionsHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Coach tạo lịch hợp lệ → trả buổi học và gọi repository đúng actor")]
    public async Task Handle_ValidSessions_ReturnsCreatedSessions()
    {
        var h = new Harness();
        var request = Request(h.ClassId);
        var expected = Response(h.ClassId, h.CoachId);
        h.Operations.Setup(x => x.CreateSessionsAsync(request, h.CoachId, h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle(request);

        Assert.Same(expected, actual);
        Assert.Equal(h.ClassId, actual.ClassId);
        Assert.Equal("Scheduled", Assert.Single(actual.Sessions).Status);
        h.Operations.Verify(x => x.CreateSessionsAsync(request, h.CoachId, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Thiếu Coach ID → từ chối, không tạo lịch")]
    public async Task Handle_MissingUserId_RejectsBeforeRepository()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        Assert.Equal("Chỉ Coach được tạo lịch lớp dạy bơi.", error.Message);
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - User không có role COACH → từ chối, không tạo lịch")]
    public async Task Handle_NonCoach_RejectsBeforeRepository()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Danh sách buổi học rỗng → ValidationException, không gọi repository")]
    public async Task Handle_EmptySessions_RejectsBeforeRepository()
    {
        var h = new Harness();

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            h.Handle(new CreateClassSessionsCommand { ClassId = h.ClassId }));

        Assert.Contains(error.Errors, x => x.PropertyName == "Sessions" && x.ErrorMessage == "Cần ít nhất một buổi học.");
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Lớp không tồn tại → truyền KeyNotFoundException")]
    public async Task Handle_ClassMissing_PropagatesNotFound()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.CreateSessionsAsync(It.IsAny<CreateClassSessionsCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy lớp dạy bơi."));

        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle());

        Assert.Equal("Không tìm thấy lớp dạy bơi.", error.Message);
    }

    [Fact(DisplayName = "UTCID06 (A) - Coach không sở hữu lớp → truyền UnauthorizedAccessException")]
    public async Task Handle_NotOwner_PropagatesUnauthorized()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.CreateSessionsAsync(It.IsAny<CreateClassSessionsCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new UnauthorizedAccessException("Chỉ Coach tạo lớp được quản lý lớp này."));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());
    }

    [Fact(DisplayName = "UTCID07 (A) - Lớp không còn Draft/Active → truyền trạng thái xung đột")]
    public async Task Handle_InvalidClassState_PropagatesConflict()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.CreateSessionsAsync(It.IsAny<CreateClassSessionsCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new SwimClassStateConflictException("Chỉ có thể thêm buổi học cho lớp Draft hoặc Active."));

        var error = await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());

        Assert.Equal("Chỉ có thể thêm buổi học cho lớp Draft hoặc Active.", error.Message);
    }

    [Fact(DisplayName = "UTCID08 (A) - Lịch bị trùng → truyền ClassSessionConflictException cùng conflicts")]
    public async Task Handle_OverlappingSchedule_PropagatesConflict()
    {
        var h = new Harness();
        var conflict = new ClassSessionConflict(0, h.CoachId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "coach_overlap");
        h.Operations.Setup(x => x.CreateSessionsAsync(It.IsAny<CreateClassSessionsCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new ClassSessionConflictException("Một hoặc nhiều buổi học bị trùng lịch.", [conflict]));

        var error = await Assert.ThrowsAsync<ClassSessionConflictException>(() => h.Handle());

        Assert.Same(conflict, Assert.Single(error.Conflicts));
    }

    [Fact(DisplayName = "UTCID09 (A) - Coach được chỉ định không hợp lệ → truyền ValidationException")]
    public async Task Handle_InvalidAssignedCoach_PropagatesValidationException()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.CreateSessionsAsync(It.IsAny<CreateClassSessionsCommand>(), h.CoachId, h.Token))
            .ThrowsAsync(new ValidationException([new ValidationFailure("Sessions[0].CoachUserId", "Buổi học phải do Coach tạo lớp phụ trách.")]));

        var error = await Assert.ThrowsAsync<ValidationException>(() => h.Handle());

        Assert.Contains(error.Errors, x => x.PropertyName == "Sessions[0].CoachUserId");
    }

    [Fact(DisplayName = "UTCID10 (A) - Repository lỗi bất ngờ → truyền tiếp exception")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Operations.Setup(x => x.CreateSessionsAsync(It.IsAny<CreateClassSessionsCommand>(), h.CoachId, h.Token)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
    }

    private static CreateClassSessionsCommand Request(Guid classId) => new()
    {
        ClassId = classId,
        Sessions = [new CreateClassSessionItem
        {
            StartsAt = new DateTimeOffset(2026, 11, 20, 6, 0, 0, TimeSpan.FromHours(7)),
            EndsAt = new DateTimeOffset(2026, 11, 20, 7, 0, 0, TimeSpan.FromHours(7))
        }]
    };

    private static CreateClassSessionsResponse Response(Guid classId, Guid coachId)
    {
        var now = DateTime.UtcNow;
        return new(classId, [new ClassSessionResponse(Guid.NewGuid(), classId, coachId,
            now.AddDays(4), now.AddDays(4).AddHours(1), "Scheduled", now, now)]);
    }

    private sealed class Harness
    {
        public readonly Guid CoachId = Guid.NewGuid();
        public readonly Guid ClassId = Guid.NewGuid();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly Mock<ISwimClassOperations> Operations = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        private readonly CreateClassSessionsHandler _handler;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CoachId);
            CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(true);
            _handler = new CreateClassSessionsHandler(Operations.Object, CurrentUser.Object,
                new CreateClassSessionsValidator(new FixedClock()));
        }

        public Task<CreateClassSessionsResponse> Handle(CreateClassSessionsCommand? request = null) =>
            _handler.Handle(request ?? Request(ClassId), Token);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
    }
}

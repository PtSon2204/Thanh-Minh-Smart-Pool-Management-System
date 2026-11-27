using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassProgressNote;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class UpdateClassProgressNoteHandlerTests
{
    [Fact]
    public async Task CoachNote_IsTrimmedAndPassedToRepository()
    {
        var coachId = Guid.NewGuid();
        var user = Coach(coachId);
        var operations = new Mock<ISwimClassOperations>();
        var command = Request("  Đã tập thở.  ");
        var now = DateTime.UtcNow;
        var expected = new ClassAttendanceResponse(Guid.NewGuid(), command.ClassId, command.SessionId,
            command.RegistrationId, null, "Absent", now, now, "Đã tập thở.");
        operations.Setup(x => x.UpdateClassProgressNoteAsync(command, coachId, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await new UpdateClassProgressNoteHandler(operations.Object, user.Object)
            .Handle(command, CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal("Đã tập thở.", command.ProgressNote);
        operations.VerifyAll();
    }

    [Fact]
    public async Task CustomerCannotWriteNote()
    {
        var operations = new Mock<ISwimClassOperations>();
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new UpdateClassProgressNoteHandler(operations.Object, user.Object)
                .Handle(Request("Đã tập thở."), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NoteLongerThanLimitIsRejected()
    {
        var operations = new Mock<ISwimClassOperations>();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new UpdateClassProgressNoteHandler(operations.Object, Coach(Guid.NewGuid()).Object)
                .Handle(Request(new string('a', 2001)), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyNoteIsRejected(string? note)
    {
        var operations = new Mock<ISwimClassOperations>();
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            new UpdateClassProgressNoteHandler(operations.Object, Coach(Guid.NewGuid()).Object)
                .Handle(Request(note), CancellationToken.None));
        Assert.Contains(error.Errors, failure => failure.PropertyName == "ProgressNote");
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingVersionIsRejected()
    {
        var operations = new Mock<ISwimClassOperations>();
        var command = Request("Đã tập thở.");
        command.ExpectedUpdatedAt = null;
        await Assert.ThrowsAsync<ValidationException>(() =>
            new UpdateClassProgressNoteHandler(operations.Object, Coach(Guid.NewGuid()).Object)
                .Handle(command, CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    private static Mock<ICurrentUser> Coach(Guid id)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(id);
        user.Setup(x => x.IsInRole("COACH")).Returns(true);
        return user;
    }

    private static UpdateClassProgressNoteCommand Request(string? note) => new()
    {
        ClassId = Guid.NewGuid(), SessionId = Guid.NewGuid(), RegistrationId = Guid.NewGuid(),
        ProgressNote = note, ExpectedUpdatedAt = DateTimeOffset.UtcNow
    };
}

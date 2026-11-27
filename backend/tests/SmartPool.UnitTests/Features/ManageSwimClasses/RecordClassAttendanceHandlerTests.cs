using FluentValidation;
using Microsoft.AspNetCore.Http;
using Moq;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RecordClassAttendance;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class RecordClassAttendanceHandlerTests
{
    private static readonly byte[] Jpeg = [0xff, 0xd8, 0x10, 0x20, 0xff, 0xd9];

    [Fact]
    public async Task CoachCapturedPhoto_IsPassedToRepository()
    {
        var coachId = Guid.NewGuid();
        var user = Coach(coachId);
        var operations = new Mock<ISwimClassOperations>();
        var request = Request("Verified", Photo(Jpeg, "image/jpeg"));
        var now = DateTime.UtcNow;
        var expected = new ClassAttendanceResponse(Guid.NewGuid(), request.ClassId, request.SessionId,
            request.RegistrationId, "https://example.test/image.jpg", "Verified", now, now);
        operations.Setup(x => x.RecordClassAttendanceAsync(request, coachId,
            It.Is<byte[]>(bytes => bytes.SequenceEqual(Jpeg)), "jpg", CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await new RecordClassAttendanceHandler(operations.Object, user.Object)
            .Handle(request, CancellationToken.None);

        Assert.Same(expected, result);
        operations.VerifyAll();
    }

    [Fact]
    public async Task CustomerCannotRecordAttendance()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        var operations = new Mock<ISwimClassOperations>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new RecordClassAttendanceHandler(operations.Object, user.Object)
                .Handle(Request("Verified", Photo(Jpeg, "image/jpeg")), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AbsentMustNotUploadPhoto()
    {
        var user = Coach(Guid.NewGuid());
        var operations = new Mock<ISwimClassOperations>();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new RecordClassAttendanceHandler(operations.Object, user.Object)
                .Handle(Request("Absent", Photo(Jpeg, "image/jpeg")), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    private static Mock<ICurrentUser> Coach(Guid id)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(id);
        user.Setup(x => x.IsInRole("COACH")).Returns(true);
        return user;
    }

    private static RecordClassAttendanceCommand Request(string status, IFormFile? image) => new()
    {
        ClassId = Guid.NewGuid(), SessionId = Guid.NewGuid(), RegistrationId = Guid.NewGuid(),
        Status = status, Image = image
    };

    private static IFormFile Photo(byte[] bytes, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "attendance.jpg")
        { Headers = new HeaderDictionary(), ContentType = contentType };
}

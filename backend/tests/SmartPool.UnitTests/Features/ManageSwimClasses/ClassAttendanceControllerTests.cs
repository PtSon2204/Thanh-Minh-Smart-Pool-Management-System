using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RecordClassAttendance;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ClassAttendanceControllerTests
{
    [Fact]
    public async Task CoachRecord_UsesRouteIdsAndReturnsOk()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var command = new RecordClassAttendanceCommand();
        var now = DateTime.UtcNow;
        var expected = new ClassAttendanceResponse(Guid.NewGuid(), classId, sessionId, registrationId,
            "https://example.test/image.jpg", "Verified", now, now);
        sender.Setup(x => x.Send(command, CancellationToken.None)).ReturnsAsync(expected);

        var result = await new ClassSessionAttendancesController(sender.Object)
            .Record(classId, sessionId, registrationId, command, CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(classId, command.ClassId);
        Assert.Equal(sessionId, command.SessionId);
        Assert.Equal(registrationId, command.RegistrationId);
        sender.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CoachRecord_InvalidImage_ReturnsBadRequest() =>
        Assert.IsType<BadRequestObjectResult>(await RecordWithError(
            new ValidationException([new ValidationFailure("Image", "Ảnh không hợp lệ.")])));

    [Fact]
    public async Task CoachRecord_NotAssigned_ReturnsForbid() =>
        Assert.IsType<ForbidResult>(await RecordWithError(new UnauthorizedAccessException()));

    [Fact]
    public async Task CoachRecord_MissingRegistration_ReturnsNotFound() =>
        Assert.IsType<NotFoundObjectResult>(await RecordWithError(new KeyNotFoundException()));

    [Fact]
    public async Task CoachRecord_ConcurrentChange_ReturnsConflict() =>
        Assert.IsType<ConflictObjectResult>(await RecordWithError(new SwimClassStateConflictException("Điểm danh đã thay đổi.")));

    private static Task<IActionResult> RecordWithError(Exception exception)
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<RecordClassAttendanceCommand>(), CancellationToken.None))
            .ThrowsAsync(exception);
        return new ClassSessionAttendancesController(sender.Object).Record(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new RecordClassAttendanceCommand(), CancellationToken.None);
    }
}

using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;
using SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ClassAttendanceControllerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Nộp ảnh thành công → HTTP 201, ClassId/SessionId lấy từ route")]
    public async Task Submit_UsesRouteIdsAndReturnsCreated()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var command = new SubmitClassAttendanceCommand { ClassId = Guid.NewGuid(), SessionId = Guid.NewGuid() };
        var now = DateTime.UtcNow;
        var expected = new ClassAttendanceResponse(Guid.NewGuid(), classId, sessionId, Guid.NewGuid(),
            "https://example.test/image.jpg", "PendingReview", now, null, null, now);
        sender.Setup(x => x.Send(command, CancellationToken.None)).ReturnsAsync(expected);

        var result = await new ClassAttendancesController(sender.Object)
            .Submit(classId, sessionId, command, CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Same(expected, created.Value);
        Assert.Equal(classId, command.ClassId);
        Assert.Equal(sessionId, command.SessionId);
        sender.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Ảnh điểm danh sai → HTTP 400")]
    public async Task Submit_InvalidImage_ReturnsBadRequest() =>
        Assert.IsType<BadRequestObjectResult>(await SubmitWithError(
            new ValidationException([new ValidationFailure("Image", "Chỉ nhận ảnh JPG/PNG tối đa 5 MB.")])));

    [Fact(DisplayName = "UTCID03 (A) - Customer không đủ quyền → HTTP 403")]
    public async Task Submit_Unauthorized_ReturnsForbid() =>
        Assert.IsType<ForbidResult>(await SubmitWithError(new UnauthorizedAccessException()));

    [Fact(DisplayName = "UTCID04 (A) - Không tìm thấy đăng ký/buổi → HTTP 404")]
    public async Task Submit_MissingRegistration_ReturnsNotFound() =>
        Assert.IsType<NotFoundObjectResult>(await SubmitWithError(
            new KeyNotFoundException("Bạn chưa đăng ký lớp này.")));

    [Fact(DisplayName = "UTCID05 (A) - Đã điểm danh hoặc quá hạn → HTTP 409")]
    public async Task Submit_Conflict_ReturnsConflict() =>
        Assert.IsType<ConflictObjectResult>(await SubmitWithError(
            new SwimClassStateConflictException("Bạn đã nộp ảnh điểm danh cho buổi học này.")));

    [Fact(DisplayName = "UTCID06 (N) - Review thành công → HTTP 200, ba ID lấy từ route")]
    public async Task Review_UsesRouteIdsAndReturnsOk()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var attendanceId = Guid.NewGuid();
        var command = new ReviewClassAttendanceCommand
        { ClassId = Guid.NewGuid(), SessionId = Guid.NewGuid(), AttendanceId = Guid.NewGuid() };
        var now = DateTime.UtcNow;
        var expected = new ReviewClassAttendanceResponse(attendanceId, classId, sessionId,
            "Verified", Guid.NewGuid(), now, null, now);
        sender.Setup(x => x.Send(command, CancellationToken.None)).ReturnsAsync(expected);

        var result = await new ClassSessionAttendancesController(sender.Object)
            .Review(classId, sessionId, attendanceId, command, CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(classId, command.ClassId);
        Assert.Equal(sessionId, command.SessionId);
        Assert.Equal(attendanceId, command.AttendanceId);
        sender.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID07 (A) - Decision review sai → HTTP 400")]
    public async Task Review_InvalidDecision_ReturnsBadRequest() =>
        Assert.IsType<BadRequestObjectResult>(await ReviewWithError(
            new ValidationException([new ValidationFailure("Decision", "Kết quả phải là Verified hoặc Rejected.")])));

    [Fact(DisplayName = "UTCID08 (A) - Coach không phụ trách buổi → HTTP 403")]
    public async Task Review_NotAssigned_ReturnsForbid() =>
        Assert.IsType<ForbidResult>(await ReviewWithError(new UnauthorizedAccessException()));

    [Fact(DisplayName = "UTCID09 (A) - Không tìm thấy điểm danh → HTTP 404")]
    public async Task Review_MissingAttendance_ReturnsNotFound() =>
        Assert.IsType<NotFoundObjectResult>(await ReviewWithError(
            new KeyNotFoundException("Không tìm thấy điểm danh của buổi học.")));

    [Fact(DisplayName = "UTCID10 (A) - Điểm danh đã review/phiên bản cũ → HTTP 409")]
    public async Task Review_Conflict_ReturnsConflict() =>
        Assert.IsType<ConflictObjectResult>(await ReviewWithError(
            new SwimClassStateConflictException("Điểm danh đã được cập nhật.")));

    private static Task<IActionResult> SubmitWithError(Exception exception)
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<SubmitClassAttendanceCommand>(), CancellationToken.None))
            .ThrowsAsync(exception);
        return new ClassAttendancesController(sender.Object).Submit(Guid.NewGuid(), Guid.NewGuid(),
            new SubmitClassAttendanceCommand(), CancellationToken.None);
    }

    private static Task<IActionResult> ReviewWithError(Exception exception)
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ReviewClassAttendanceCommand>(), CancellationToken.None))
            .ThrowsAsync(exception);
        return new ClassSessionAttendancesController(sender.Object).Review(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new ReviewClassAttendanceCommand(), CancellationToken.None);
    }
}

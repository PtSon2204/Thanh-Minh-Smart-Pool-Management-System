using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassSession;
using SmartPool.Application.Features.ManageSwimClasses.Commands.DeactivateSwimClass;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class SwimClassStopControllerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Ngừng lớp thành công → HTTP 200, ClassId lấy từ route")]
    public async Task Deactivate_UsesRouteIdAndReturnsOk()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var command = new DeactivateSwimClassCommand { ClassId = Guid.NewGuid() };
        var now = DateTime.UtcNow;
        var response = new DeactivateSwimClassResponse(classId, "Inactive", "Bảo trì", now, now);
        sender.Setup(x => x.Send(command, CancellationToken.None)).ReturnsAsync(response);

        var result = await Controller(sender).Deactivate(classId, command, CancellationToken.None);

        Assert.Same(response, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(classId, command.ClassId);
        sender.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Hủy tiết thành công → HTTP 200, ClassId và SessionId lấy từ route")]
    public async Task CancelSession_UsesRouteIdsAndReturnsOk()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var command = new CancelClassSessionCommand { ClassId = Guid.NewGuid(), SessionId = Guid.NewGuid() };
        var response = new CancelClassSessionResponse(sessionId, classId, "Cancelled", "Coach nghỉ", DateTime.UtcNow);
        sender.Setup(x => x.Send(command, CancellationToken.None)).ReturnsAsync(response);

        var result = await Controller(sender).CancelSession(classId, sessionId, command, CancellationToken.None);

        Assert.Same(response, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(classId, command.ClassId);
        Assert.Equal(sessionId, command.SessionId);
        sender.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (A) - Ngừng lớp thiếu user ID → HTTP 403, không gửi command")]
    public async Task Deactivate_MissingUserId_ReturnsForbid()
    {
        var sender = new Mock<ISender>();

        var result = await Controller(sender, includeUserId: false).Deactivate(
            Guid.NewGuid(), new DeactivateSwimClassCommand(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Hủy tiết thiếu user ID → HTTP 403, không gửi command")]
    public async Task CancelSession_MissingUserId_ReturnsForbid()
    {
        var sender = new Mock<ISender>();

        var result = await Controller(sender, includeUserId: false).CancelSession(
            Guid.NewGuid(), Guid.NewGuid(), new CancelClassSessionCommand(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Lý do ngừng lớp sai → HTTP 400")]
    public async Task Deactivate_InvalidReason_ReturnsBadRequest()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<DeactivateSwimClassCommand>(), CancellationToken.None))
            .ThrowsAsync(new ValidationException([new ValidationFailure("Reason", "Lý do không hợp lệ.")]));

        var result = await Controller(sender).Deactivate(Guid.NewGuid(),
            new DeactivateSwimClassCommand(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "UTCID06 (A) - Lý do hủy tiết sai → HTTP 400")]
    public async Task CancelSession_InvalidReason_ReturnsBadRequest()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CancelClassSessionCommand>(), CancellationToken.None))
            .ThrowsAsync(new ValidationException([new ValidationFailure("Reason", "Lý do không hợp lệ.")]));

        var result = await Controller(sender).CancelSession(Guid.NewGuid(), Guid.NewGuid(),
            new CancelClassSessionCommand(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "UTCID07 (A) - Coach không sở hữu lớp → ngừng lớp trả HTTP 403")]
    public async Task Deactivate_NotOwner_ReturnsForbid()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<DeactivateSwimClassCommand>(), CancellationToken.None))
            .ThrowsAsync(new UnauthorizedAccessException());

        Assert.IsType<ForbidResult>(await Controller(sender).Deactivate(Guid.NewGuid(),
            new DeactivateSwimClassCommand(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID08 (A) - Coach không sở hữu lớp → hủy tiết trả HTTP 403")]
    public async Task CancelSession_NotOwner_ReturnsForbid()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CancelClassSessionCommand>(), CancellationToken.None))
            .ThrowsAsync(new UnauthorizedAccessException());

        Assert.IsType<ForbidResult>(await Controller(sender).CancelSession(Guid.NewGuid(), Guid.NewGuid(),
            new CancelClassSessionCommand(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID09 (A) - Lớp không tồn tại → ngừng lớp trả HTTP 404")]
    public async Task Deactivate_ClassMissing_ReturnsNotFound()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<DeactivateSwimClassCommand>(), CancellationToken.None))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy lớp dạy bơi."));

        Assert.IsType<NotFoundObjectResult>(await Controller(sender).Deactivate(Guid.NewGuid(),
            new DeactivateSwimClassCommand(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID10 (A) - Tiết không thuộc lớp → hủy tiết trả HTTP 404")]
    public async Task CancelSession_SessionMissing_ReturnsNotFound()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CancelClassSessionCommand>(), CancellationToken.None))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy buổi học của lớp."));

        Assert.IsType<NotFoundObjectResult>(await Controller(sender).CancelSession(Guid.NewGuid(), Guid.NewGuid(),
            new CancelClassSessionCommand(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID11 (A) - Phiên bản lớp đã cũ → ngừng lớp trả HTTP 409")]
    public async Task Deactivate_StaleVersion_ReturnsConflict()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<DeactivateSwimClassCommand>(), CancellationToken.None))
            .ThrowsAsync(new SwimClassStateConflictException("Lớp đã được cập nhật."));

        Assert.IsType<ConflictObjectResult>(await Controller(sender).Deactivate(Guid.NewGuid(),
            new DeactivateSwimClassCommand(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID12 (A) - Tiết đã hủy → hủy tiết trả HTTP 409")]
    public async Task CancelSession_AlreadyCancelled_ReturnsConflict()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CancelClassSessionCommand>(), CancellationToken.None))
            .ThrowsAsync(new SwimClassStateConflictException("Chỉ có thể hủy buổi Scheduled chưa bắt đầu."));

        Assert.IsType<ConflictObjectResult>(await Controller(sender).CancelSession(Guid.NewGuid(), Guid.NewGuid(),
            new CancelClassSessionCommand(), CancellationToken.None));
    }

    private static SwimClassesController Controller(Mock<ISender> sender, bool includeUserId = true)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, "COACH") };
        if (includeUserId) claims.Add(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        return new SwimClassesController(sender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }
}

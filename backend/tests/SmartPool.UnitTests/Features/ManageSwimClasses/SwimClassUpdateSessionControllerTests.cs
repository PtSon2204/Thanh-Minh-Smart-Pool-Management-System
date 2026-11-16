using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class SwimClassUpdateSessionControllerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Sửa lịch thành công → HTTP 200 và lấy ClassId, SessionId từ route")]
    public async Task UpdateSession_UsesRouteIdsAndReturnsOk()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var command = new UpdateClassSessionCommand { ClassId = Guid.NewGuid(), SessionId = Guid.NewGuid() };
        var now = DateTime.UtcNow;
        var response = new UpdateClassSessionResponse(sessionId, classId, Guid.NewGuid(), now, now.AddHours(1),
            "Scheduled", now.AddDays(-1), now);
        sender.Setup(x => x.Send(command, CancellationToken.None)).ReturnsAsync(response);

        var result = await Controller(sender).UpdateSession(classId, sessionId, command, CancellationToken.None);

        Assert.Same(response, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(classId, command.ClassId);
        Assert.Equal(sessionId, command.SessionId);
        sender.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Thiếu user ID → HTTP 403 và không gửi command")]
    public async Task UpdateSession_MissingUserId_ReturnsForbid()
    {
        var sender = new Mock<ISender>();

        var result = await Controller(sender, includeUserId: false).UpdateSession(
            Guid.NewGuid(), Guid.NewGuid(), new UpdateClassSessionCommand(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - Dữ liệu thời gian sai → HTTP 400 với lỗi validation")]
    public async Task UpdateSession_InvalidTime_ReturnsBadRequest()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<UpdateClassSessionCommand>(), CancellationToken.None))
            .ThrowsAsync(new ValidationException([new ValidationFailure("StartsAt", "Buổi học phải bắt đầu trong tương lai.")]));

        var result = await Controller(sender).UpdateSession(Guid.NewGuid(), Guid.NewGuid(),
            new UpdateClassSessionCommand(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "UTCID04 (A) - Coach không sở hữu lớp → HTTP 403")]
    public async Task UpdateSession_NotOwner_ReturnsForbid()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<UpdateClassSessionCommand>(), CancellationToken.None))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await Controller(sender).UpdateSession(Guid.NewGuid(), Guid.NewGuid(),
            new UpdateClassSessionCommand(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact(DisplayName = "UTCID05 (A) - Buổi học không thuộc lớp → HTTP 404")]
    public async Task UpdateSession_MissingSession_ReturnsNotFound()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<UpdateClassSessionCommand>(), CancellationToken.None))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy buổi học của lớp."));

        var result = await Controller(sender).UpdateSession(Guid.NewGuid(), Guid.NewGuid(),
            new UpdateClassSessionCommand(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact(DisplayName = "UTCID06 (A) - Phiên bản đã cũ → HTTP 409")]
    public async Task UpdateSession_StaleVersion_ReturnsConflict()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<UpdateClassSessionCommand>(), CancellationToken.None))
            .ThrowsAsync(new SwimClassStateConflictException("Buổi học đã được người khác cập nhật."));

        var result = await Controller(sender).UpdateSession(Guid.NewGuid(), Guid.NewGuid(),
            new UpdateClassSessionCommand(), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact(DisplayName = "UTCID07 (A) - Lịch mới trùng lịch Coach → HTTP 409 có conflicts")]
    public async Task UpdateSession_Overlap_ReturnsConflictDetails()
    {
        var sender = new Mock<ISender>();
        var conflict = new ClassSessionConflict(0, Guid.NewGuid(), DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1), "coach_overlap");
        sender.Setup(x => x.Send(It.IsAny<UpdateClassSessionCommand>(), CancellationToken.None))
            .ThrowsAsync(new ClassSessionConflictException("Buổi học bị trùng lịch.", [conflict]));

        var result = await Controller(sender).UpdateSession(Guid.NewGuid(), Guid.NewGuid(),
            new UpdateClassSessionCommand(), CancellationToken.None);

        var payload = Assert.IsType<ConflictObjectResult>(result).Value;
        Assert.Contains("conflicts", payload!.GetType().GetProperties().Select(x => x.Name));
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

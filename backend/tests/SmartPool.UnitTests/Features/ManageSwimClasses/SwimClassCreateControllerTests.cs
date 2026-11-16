using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class SwimClassCreateControllerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Tạo lớp thành công → HTTP 201 với thông tin lớp")]
    public async Task Create_ReturnsCreated()
    {
        var sender = new Mock<ISender>();
        var command = new CreateSwimClassCommand();
        var now = DateTime.UtcNow;
        var response = new CreateSwimClassResponse(Guid.NewGuid(), "Lớp bơi", null, 5, 100000m,
            Guid.NewGuid(), now, now.AddDays(1), "Draft", Guid.NewGuid(), now, now);
        sender.Setup(x => x.Send(command, CancellationToken.None)).ReturnsAsync(response);

        var result = await Controller(sender).Create(command, CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Same(response, created.Value);
        sender.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Tạo lịch thành công → HTTP 201, ClassId lấy từ route")]
    public async Task CreateSessions_UsesRouteClassIdAndReturnsCreated()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var command = new CreateClassSessionsCommand { ClassId = Guid.NewGuid() };
        var response = new CreateClassSessionsResponse(classId, []);
        sender.Setup(x => x.Send(command, CancellationToken.None)).ReturnsAsync(response);

        var result = await Controller(sender).CreateSessions(classId, command, CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Same(response, created.Value);
        Assert.Equal(classId, command.ClassId);
        sender.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiếu user ID → HTTP 403, không gửi command")]
    public async Task Create_MissingUserId_ReturnsForbid()
    {
        var sender = new Mock<ISender>();

        var result = await Controller(sender, includeUserId: false).Create(new CreateSwimClassCommand(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Dữ liệu lớp sai → HTTP 400 với lỗi validation")]
    public async Task Create_ValidationFailure_ReturnsBadRequest()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CreateSwimClassCommand>(), CancellationToken.None))
            .ThrowsAsync(new ValidationException([new ValidationFailure("Capacity", "Sức chứa phải từ 1 đến 5 học viên.")]));

        var result = await Controller(sender).Create(new CreateSwimClassCommand(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "UTCID05 (A) - Coach không được quản lý lớp → HTTP 403")]
    public async Task CreateSessions_NotOwner_ReturnsForbid()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CreateClassSessionsCommand>(), CancellationToken.None))
            .ThrowsAsync(new UnauthorizedAccessException());

        Assert.IsType<ForbidResult>(await Controller(sender).CreateSessions(Guid.NewGuid(),
            new CreateClassSessionsCommand(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID06 (A) - Lịch bị trùng → HTTP 409 với conflicts")]
    public async Task CreateSessions_Overlap_ReturnsConflict()
    {
        var sender = new Mock<ISender>();
        var conflict = new ClassSessionConflict(0, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "coach_overlap");
        sender.Setup(x => x.Send(It.IsAny<CreateClassSessionsCommand>(), CancellationToken.None))
            .ThrowsAsync(new ClassSessionConflictException("Một hoặc nhiều buổi học bị trùng lịch.", [conflict]));

        var result = await Controller(sender).CreateSessions(Guid.NewGuid(), new CreateClassSessionsCommand(), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact(DisplayName = "UTCID07 (A) - Lớp không tồn tại → HTTP 404")]
    public async Task CreateSessions_MissingClass_ReturnsNotFound()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CreateClassSessionsCommand>(), CancellationToken.None))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy lớp dạy bơi."));

        Assert.IsType<NotFoundObjectResult>(await Controller(sender).CreateSessions(Guid.NewGuid(),
            new CreateClassSessionsCommand(), CancellationToken.None));
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

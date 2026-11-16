using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class SwimClassRegistrationControllerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Customer gửi yêu cầu → HTTP 201 với PendingApproval")]
    public async Task Register_ValidRequest_ReturnsCreated()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var expected = new RegisterForSwimClassResponse(Guid.NewGuid(), classId,
            "PendingApproval", DateTime.UtcNow, null);
        sender.Setup(x => x.Send(It.Is<RegisterForSwimClassCommand>(q => q.ClassId == classId),
            CancellationToken.None)).ReturnsAsync(expected);

        var result = await Controller(sender).Register(classId, CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Same(expected, created.Value);
        sender.Verify(x => x.Send(It.Is<RegisterForSwimClassCommand>(q => q.ClassId == classId),
            CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Gửi yêu cầu thiếu user ID → HTTP 403, không gửi command")]
    public async Task Register_MissingUserId_ReturnsForbid()
    {
        var sender = new Mock<ISender>();

        Assert.IsType<ForbidResult>(await Controller(sender, includeUserId: false)
            .Register(Guid.NewGuid(), CancellationToken.None));
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - Lớp không tồn tại → HTTP 404")]
    public async Task Register_MissingClass_ReturnsNotFound()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<RegisterForSwimClassCommand>(), CancellationToken.None))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy lớp dạy bơi."));

        Assert.IsType<NotFoundObjectResult>(await Controller(sender)
            .Register(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID04 (A) - Đăng ký trùng hoặc lớp hết chỗ → HTTP 409")]
    public async Task Register_Conflict_ReturnsConflict()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<RegisterForSwimClassCommand>(), CancellationToken.None))
            .ThrowsAsync(new SwimClassStateConflictException("Lớp đã hết chỗ đăng ký."));

        Assert.IsType<ConflictObjectResult>(await Controller(sender)
            .Register(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID05 (N) - Coach review thành công → HTTP 200 và ID lấy từ route")]
    public async Task Review_UsesRouteIdsAndReturnsOk()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var command = new ReviewClassRegistrationCommand { ClassId = Guid.NewGuid(), RegistrationId = Guid.NewGuid() };
        var now = DateTime.UtcNow;
        var expected = new ReviewClassRegistrationResponse(registrationId, classId,
            "Confirmed", Guid.NewGuid(), now, null, now, now);
        sender.Setup(x => x.Send(command, CancellationToken.None)).ReturnsAsync(expected);

        var result = await Controller(sender, role: "COACH")
            .ReviewRegistration(classId, registrationId, command, CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(classId, command.ClassId);
        Assert.Equal(registrationId, command.RegistrationId);
        sender.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID06 (A) - Decision review sai → HTTP 400")]
    public async Task Review_InvalidDecision_ReturnsBadRequest()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ReviewClassRegistrationCommand>(), CancellationToken.None))
            .ThrowsAsync(new ValidationException([new ValidationFailure("Decision", "Quyết định phải là Accepted hoặc Rejected.")]));

        Assert.IsType<BadRequestObjectResult>(await Controller(sender, role: "COACH")
            .ReviewRegistration(Guid.NewGuid(), Guid.NewGuid(), new ReviewClassRegistrationCommand(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID07 (A) - Coach không sở hữu lớp → HTTP 403")]
    public async Task Review_NotOwner_ReturnsForbid()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ReviewClassRegistrationCommand>(), CancellationToken.None))
            .ThrowsAsync(new UnauthorizedAccessException());

        Assert.IsType<ForbidResult>(await Controller(sender, role: "COACH")
            .ReviewRegistration(Guid.NewGuid(), Guid.NewGuid(), new ReviewClassRegistrationCommand(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID08 (A) - Yêu cầu tham gia không tồn tại → HTTP 404")]
    public async Task Review_MissingRegistration_ReturnsNotFound()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ReviewClassRegistrationCommand>(), CancellationToken.None))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy đăng ký của lớp."));

        Assert.IsType<NotFoundObjectResult>(await Controller(sender, role: "COACH")
            .ReviewRegistration(Guid.NewGuid(), Guid.NewGuid(), new ReviewClassRegistrationCommand(), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID09 (A) - Lớp hết chỗ khi review → HTTP 409")]
    public async Task Review_FullClass_ReturnsConflict()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ReviewClassRegistrationCommand>(), CancellationToken.None))
            .ThrowsAsync(new SwimClassStateConflictException("Lớp đã hết chỗ; yêu cầu vẫn đang chờ duyệt."));

        Assert.IsType<ConflictObjectResult>(await Controller(sender, role: "COACH")
            .ReviewRegistration(Guid.NewGuid(), Guid.NewGuid(), new ReviewClassRegistrationCommand(), CancellationToken.None));
    }

    private static SwimClassesController Controller(Mock<ISender> sender, bool includeUserId = true,
        string role = "CUSTOMER")
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
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

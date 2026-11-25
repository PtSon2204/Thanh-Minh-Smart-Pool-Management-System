using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageCoachApplications;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CoachApplicationsControllerTests
{
    [Fact]
    public async Task Submit_DelegatesFileToApplication_AndReturnsCreated()
    {
        var sender = new Mock<ISender>();
        var item = Item();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var file = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes),
            0, bytes.Length, "certificate", "certificate.jpg");
        sender.Setup(x => x.Send(It.Is<SubmitCoachApplicationCommand>(command =>
                ReferenceEquals(command.Certificate, file)),
            CancellationToken.None)).ReturnsAsync(item);

        var result = await new CoachApplicationsController(sender.Object)
            .Submit(file, CancellationToken.None);

        Assert.Same(item, Assert.IsType<CreatedAtActionResult>(result).Value);
        sender.VerifyAll();
    }

    [Fact]
    public async Task Review_DelegatesDecisionToApplication_AndReturnsOk()
    {
        var sender = new Mock<ISender>();
        var item = Item();
        sender.Setup(x => x.Send(It.Is<ReviewCoachApplicationCommand>(command =>
                command.Id == item.Id && command.Decision == " approved " && command.Note == "Đạt"),
            CancellationToken.None)).ReturnsAsync(item);

        var result = await new CoachApplicationsController(sender.Object).Review(item.Id,
            new CoachApplicationsController.ReviewRequest(" approved ", "Đạt"),
            CancellationToken.None);

        Assert.Same(item, Assert.IsType<OkObjectResult>(result).Value);
        sender.VerifyAll();
    }

    [Fact]
    public async Task ValidationAndAuthorizationFailures_MapToHttp()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ReviewCoachApplicationCommand>(),
                CancellationToken.None))
            .ThrowsAsync(new ValidationException([new ValidationFailure("Decision", "Sai quyết định")]));
        sender.Setup(x => x.Send(It.IsAny<GetCoachCertificateQuery>(),
                CancellationToken.None))
            .ThrowsAsync(new UnauthorizedAccessException());
        var controller = new CoachApplicationsController(sender.Object);

        Assert.IsType<BadRequestObjectResult>(await controller.Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Pending", null), CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.GetCertificate(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task MissingApplicationAndRepositoryConflict_MapToHttp()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<GetCoachApplicationByIdQuery>(),
                CancellationToken.None))
            .ReturnsAsync((CoachApplicationItem?)null);
        sender.Setup(x => x.Send(It.IsAny<ReviewCoachApplicationCommand>(),
                CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Yêu cầu này đã được xử lý."));
        var controller = new CoachApplicationsController(sender.Object);

        Assert.IsType<NotFoundResult>(await controller.GetById(Guid.NewGuid(), CancellationToken.None));
        Assert.IsType<ConflictObjectResult>(await controller.Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Approved", null), CancellationToken.None));
    }

    [Fact]
    public async Task RevokeRequiresReasonBeforeChangingAccess()
    {
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        var controller = UserCoachController(operations);

        var response = await controller.Revoke(Guid.NewGuid(),
            new UserCoachAccessController.RevokeCoachRequest("  "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UserManagementRevokeUsesTargetUserAndCurrentAdmin()
    {
        var targetUserId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        var application = Item() with { UserId = targetUserId, Status = "Revoked" };
        operations.Setup(item => item.RevokeForUserAsync(targetUserId, adminId,
                "Không còn phụ trách lớp", CancellationToken.None))
            .ReturnsAsync(application);

        var response = await UserCoachController(operations, adminId).Revoke(targetUserId,
            new UserCoachAccessController.RevokeCoachRequest("Không còn phụ trách lớp"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        operations.VerifyAll();
    }

    private static UserCoachAccessController UserCoachController(
        Mock<ICoachApplicationOperations> operations, Guid? adminId = null)
    {
        return new UserCoachAccessController(operations.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(
                            [new System.Security.Claims.Claim(
                                System.Security.Claims.ClaimTypes.NameIdentifier,
                                (adminId ?? Guid.NewGuid()).ToString())], "test"))
                }
            }
        };
    }

    private static CoachApplicationItem Item() => new(Guid.NewGuid(), Guid.NewGuid(),
        "Customer", "Pending", DateTime.UtcNow, null, null, null, null, null, null);
}

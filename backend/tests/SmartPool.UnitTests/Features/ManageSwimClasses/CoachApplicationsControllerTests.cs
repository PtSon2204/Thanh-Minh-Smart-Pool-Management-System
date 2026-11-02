using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CoachApplicationsControllerTests
{
    [Fact]
    public async Task SubmitRejectsNonImageContentBeforeStorage()
    {
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        var controller = CreateController(operations.Object);
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length,
            "certificate", "certificate.jpg") { Headers = new HeaderDictionary(),
                ContentType = "image/jpeg" };

        var response = await controller.Submit(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CertificateEndpointDoesNotReturnAnotherUsersCertificate()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        operations.Setup(item => item.GetCertificateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var controller = CreateController(operations.Object);

        var response = await controller.GetCertificate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
    }

    [Fact]
    public async Task CustomerDetailOnlyLooksUpTheirOwnApplication()
    {
        var applicationId = Guid.NewGuid();
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        var controller = CreateController(operations.Object);
        var userId = Guid.Parse(controller.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        operations.Setup(item => item.GetByIdForUserAsync(applicationId, userId,
                CancellationToken.None))
            .ReturnsAsync((CoachApplicationItem?)null);

        var response = await controller.GetById(applicationId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(response);
        operations.VerifyAll();
    }

    [Fact]
    public async Task RevokeRequiresReasonBeforeChangingAccess()
    {
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        var controller = new UserCoachAccessController(operations.Object)
        {
            ControllerContext = CreateController(operations.Object).ControllerContext
        };

        var response = await controller.Revoke(Guid.NewGuid(),
            new UserCoachAccessController.RevokeCoachRequest("  "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UserManagementRevokeUsesTargetUserAndCurrentAdmin()
    {
        var targetUserId = Guid.NewGuid();
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        var controller = new UserCoachAccessController(operations.Object)
        {
            ControllerContext = CreateController(operations.Object).ControllerContext
        };
        var adminId = Guid.Parse(controller.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var application = new CoachApplicationItem(Guid.NewGuid(), targetUserId, "Coach",
            "Revoked", DateTime.UtcNow, adminId, DateTime.UtcNow, null,
            adminId, DateTime.UtcNow, "Không còn phụ trách lớp");
        operations.Setup(item => item.RevokeForUserAsync(targetUserId, adminId,
                "Không còn phụ trách lớp", CancellationToken.None))
            .ReturnsAsync(application);

        var response = await controller.Revoke(targetUserId,
            new UserCoachAccessController.RevokeCoachRequest("Không còn phụ trách lớp"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        operations.VerifyAll();
    }

    private static CoachApplicationsController CreateController(ICoachApplicationOperations operations) =>
        new(operations)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                         new Claim(ClaimTypes.Role, "CUSTOMER")], "test"))
                }
            }
        };
}

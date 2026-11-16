using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CoachApplicationReviewTests
{
    [Fact(DisplayName = "UTCID01 (N) - Admin duyệt yêu cầu → HTTP 200, chuẩn hóa Approved và đúng admin")]
    public async Task Review_Approve_ReturnsOk()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        var adminId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var expected = Item(applicationId, adminId, "Approved");
        operations.Setup(x => x.ReviewAsync(applicationId, adminId, "Approved", "Đạt", CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await Controller(operations, adminId).Review(applicationId,
            new CoachApplicationsController.ReviewRequest(" approved ", "Đạt"), CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        operations.Verify(x => x.ReviewAsync(applicationId, adminId, "Approved", "Đạt", CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Admin từ chối với ghi chú → HTTP 200, truyền Rejected")]
    public async Task Review_RejectWithNote_ReturnsOk()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        var adminId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var expected = Item(applicationId, adminId, "Rejected");
        operations.Setup(x => x.ReviewAsync(applicationId, adminId, "Rejected", "Ảnh mờ", CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await Controller(operations, adminId).Review(applicationId,
            new CoachApplicationsController.ReviewRequest("REJECTED", "Ảnh mờ"), CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        operations.VerifyAll();
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiếu admin ID → HTTP 403, không review")]
    public async Task Review_MissingAdminId_ReturnsForbid()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        var result = await Controller(operations, includeUserId: false).Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Approved", null), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Decision không hợp lệ → HTTP 400, không review")]
    public async Task Review_InvalidDecision_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        Assert.IsType<BadRequestObjectResult>(await Controller(operations).Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Pending", null), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Decision rỗng → HTTP 400, không review")]
    public async Task Review_EmptyDecision_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        Assert.IsType<BadRequestObjectResult>(await Controller(operations).Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest(" ", null), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Từ chối thiếu ghi chú → HTTP 400, không review")]
    public async Task Review_RejectWithoutNote_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        Assert.IsType<BadRequestObjectResult>(await Controller(operations).Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Rejected", "  "), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID07 (B) - Ghi chú 1001 ký tự → HTTP 400, không review")]
    public async Task Review_NoteTooLong_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        Assert.IsType<BadRequestObjectResult>(await Controller(operations).Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Approved", new string('a', 1001)), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID08 (B) - Ghi chú đúng 1000 ký tự → được review")]
    public async Task Review_NoteAtLimit_IsAccepted()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        var adminId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var note = new string('a', 1000);
        operations.Setup(x => x.ReviewAsync(applicationId, adminId, "Approved", note, CancellationToken.None))
            .ReturnsAsync(Item(applicationId, adminId, "Approved"));

        var result = await Controller(operations, adminId).Review(applicationId,
            new CoachApplicationsController.ReviewRequest("Approved", note), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        operations.VerifyAll();
    }

    [Fact(DisplayName = "UTCID09 (A) - Yêu cầu không tồn tại → HTTP 404")]
    public async Task Review_MissingApplication_ReturnsNotFound()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        operations.Setup(x => x.ReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), "Approved", null, CancellationToken.None))
            .ReturnsAsync((CoachApplicationItem?)null);

        Assert.IsType<NotFoundResult>(await Controller(operations).Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Approved", null), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID10 (A) - Yêu cầu đã xử lý → HTTP 409")]
    public async Task Review_AlreadyProcessed_ReturnsConflict()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        operations.Setup(x => x.ReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), "Approved", null, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Yêu cầu này đã được xử lý."));

        Assert.IsType<ConflictObjectResult>(await Controller(operations).Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Approved", null), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID11 (A) - Customer không còn đủ điều kiện → HTTP 409")]
    public async Task Review_IneligibleCustomer_ReturnsConflict()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        operations.Setup(x => x.ReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), "Approved", null, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Tài khoản không còn đủ điều kiện nhận quyền Coach."));

        Assert.IsType<ConflictObjectResult>(await Controller(operations).Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Approved", null), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID12 (A) - Role COACH chưa khả dụng → HTTP 409")]
    public async Task Review_CoachRoleUnavailable_ReturnsConflict()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        operations.Setup(x => x.ReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), "Approved", null, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Role COACH chưa khả dụng."));

        Assert.IsType<ConflictObjectResult>(await Controller(operations).Review(Guid.NewGuid(),
            new CoachApplicationsController.ReviewRequest("Approved", null), CancellationToken.None));
    }

    private static CoachApplicationItem Item(Guid id, Guid adminId, string status) =>
        new(id, Guid.NewGuid(), "Customer", status, DateTime.UtcNow,
            adminId, DateTime.UtcNow, null, null, null, null);

    private static CoachApplicationsController Controller(Mock<ICoachApplicationOperations> operations,
        Guid? adminId = null, bool includeUserId = true)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, "ADMIN") };
        if (includeUserId) claims.Add(new Claim(ClaimTypes.NameIdentifier, (adminId ?? Guid.NewGuid()).ToString()));
        return new CoachApplicationsController(operations.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CoachApplicationSubmitTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0x10, 0x20, 0xFF, 0xD9];
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 1];

    [Fact(DisplayName = "UTCID01 (N) - Nộp chứng chỉ JPG hợp lệ → HTTP 201, gửi đúng ảnh và user")]
    public async Task Submit_ValidJpeg_ReturnsCreated()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        var userId = Guid.NewGuid();
        var expected = Item(userId);
        operations.Setup(x => x.SubmitAsync(userId, It.Is<byte[]>(b => b.SequenceEqual(Jpeg)),
            "jpg", CancellationToken.None)).ReturnsAsync(expected);

        var result = await Controller(operations, userId).Submit(File(Jpeg, "image/jpeg"), CancellationToken.None);

        Assert.Same(expected, Assert.IsType<CreatedAtActionResult>(result).Value);
        operations.Verify(x => x.SubmitAsync(userId, It.Is<byte[]>(b => b.SequenceEqual(Jpeg)),
            "jpg", CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Nộp chứng chỉ PNG hợp lệ → HTTP 201 và format png")]
    public async Task Submit_ValidPng_ReturnsCreated()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        var userId = Guid.NewGuid();
        operations.Setup(x => x.SubmitAsync(userId, It.Is<byte[]>(b => b.SequenceEqual(Png)),
            "png", CancellationToken.None)).ReturnsAsync(Item(userId));

        var result = await Controller(operations, userId).Submit(File(Png, "image/png"), CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        operations.Verify(x => x.SubmitAsync(userId, It.Is<byte[]>(b => b.SequenceEqual(Png)),
            "png", CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiếu user ID → HTTP 403, không nộp yêu cầu")]
    public async Task Submit_MissingUserId_ReturnsForbid()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        var result = await Controller(operations, includeUserId: false)
            .Submit(File(Jpeg, "image/jpeg"), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Không đính kèm chứng chỉ → HTTP 400, không lưu")]
    public async Task Submit_NoCertificate_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        Assert.IsType<BadRequestObjectResult>(await Controller(operations).Submit(null, CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (B) - Ảnh dưới 4 byte → HTTP 400, không lưu")]
    public async Task Submit_TooSmall_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        Assert.IsType<BadRequestObjectResult>(await Controller(operations)
            .Submit(File([0xFF, 0xD8, 0xD9], "image/jpeg"), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (B) - Ảnh trên 5 MB → HTTP 400, không lưu")]
    public async Task Submit_TooLarge_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        var bytes = new byte[5 * 1024 * 1024 + 1];

        Assert.IsType<BadRequestObjectResult>(await Controller(operations)
            .Submit(File(bytes, "image/png"), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID07 (A) - MIME không phải JPG/PNG → HTTP 400, không lưu")]
    public async Task Submit_UnsupportedMime_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        Assert.IsType<BadRequestObjectResult>(await Controller(operations)
            .Submit(File(Jpeg, "image/gif"), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID08 (A) - Nội dung ảnh không khớp MIME → HTTP 400, không lưu")]
    public async Task Submit_MimeMismatch_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        Assert.IsType<BadRequestObjectResult>(await Controller(operations)
            .Submit(File(Png, "image/jpeg"), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID09 (A) - Dữ liệu không có chữ ký ảnh → HTTP 400, không lưu")]
    public async Task Submit_InvalidImageBytes_ReturnsBadRequest()
    {
        var operations = new Mock<ICoachApplicationOperations>();

        Assert.IsType<BadRequestObjectResult>(await Controller(operations)
            .Submit(File([1, 2, 3, 4, 5], "image/jpeg"), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID10 (A) - Customer không còn đủ điều kiện → HTTP 403")]
    public async Task Submit_IneligibleCustomer_ReturnsForbid()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        operations.Setup(x => x.SubmitAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), "jpg", CancellationToken.None))
            .ThrowsAsync(new UnauthorizedAccessException("Chỉ Customer đang hoạt động được nộp chứng chỉ."));

        Assert.IsType<ForbidResult>(await Controller(operations)
            .Submit(File(Jpeg, "image/jpeg"), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID11 (A) - Đã có yêu cầu chờ duyệt → HTTP 409")]
    public async Task Submit_PendingApplication_ReturnsConflict()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        operations.Setup(x => x.SubmitAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), "jpg", CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Bạn đã có yêu cầu đang chờ duyệt."));

        Assert.IsType<ConflictObjectResult>(await Controller(operations)
            .Submit(File(Jpeg, "image/jpeg"), CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID12 (A) - Đã có quyền Coach → HTTP 409")]
    public async Task Submit_AlreadyCoach_ReturnsConflict()
    {
        var operations = new Mock<ICoachApplicationOperations>();
        operations.Setup(x => x.SubmitAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), "jpg", CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Bạn đã có quyền Coach."));

        Assert.IsType<ConflictObjectResult>(await Controller(operations)
            .Submit(File(Jpeg, "image/jpeg"), CancellationToken.None));
    }

    private static CoachApplicationItem Item(Guid userId) => new(Guid.NewGuid(), userId,
        "Customer", "Pending", DateTime.UtcNow, null, null, null, null, null, null);

    private static IFormFile File(byte[] bytes, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "certificate", "certificate.jpg")
        { Headers = new HeaderDictionary(), ContentType = contentType };

    private static CoachApplicationsController Controller(Mock<ICoachApplicationOperations> operations,
        Guid? userId = null, bool includeUserId = true)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, "CUSTOMER") };
        if (includeUserId) claims.Add(new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()));
        return new CoachApplicationsController(operations.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }
}

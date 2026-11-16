using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class SwimClassReadControllerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Danh sách hợp lệ → HTTP 200 với trang dữ liệu")]
    public async Task GetList_ReturnsOk()
    {
        var sender = new Mock<ISender>();
        var query = new GetSwimClassesQuery();
        var page = new PagedResponse<SwimClassListItem> { PageIndex = 1, PageSize = 10 };
        sender.Setup(x => x.Send(query, CancellationToken.None)).ReturnsAsync(page);

        var result = await Controller(sender).GetList(query, CancellationToken.None);

        Assert.Same(page, Assert.IsType<OkObjectResult>(result).Value);
        sender.Verify(x => x.Send(query, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Xem chi tiết hợp lệ → HTTP 200, chuyển ClassId và scope")]
    public async Task GetById_ReturnsOk()
    {
        var sender = new Mock<ISender>();
        var classId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var detail = new SwimClassDetailResponse(classId, "Lớp bơi", null, 5, 100000m,
            Guid.NewGuid(), "Coach", now, now.AddDays(1), "Active", Guid.NewGuid(), 0, now, now, []);
        sender.Setup(x => x.Send(It.Is<GetSwimClassByIdQuery>(q => q.ClassId == classId && q.Scope == "COACH"),
            CancellationToken.None)).ReturnsAsync(detail);

        var result = await Controller(sender).GetById(classId, CancellationToken.None, "COACH");

        Assert.Same(detail, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact(DisplayName = "UTCID03 (A) - Query danh sách sai → HTTP 400 với lỗi validation")]
    public async Task GetList_ValidationFailure_ReturnsBadRequest()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<GetSwimClassesQuery>(), CancellationToken.None))
            .ThrowsAsync(new ValidationException([new ValidationFailure("PageSize", "Giá trị không hợp lệ.")]));

        var result = await Controller(sender).GetList(new GetSwimClassesQuery { PageSize = 0 }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Giá trị không hợp lệ.", System.Text.Json.JsonSerializer.Serialize(badRequest.Value,
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    [Fact(DisplayName = "UTCID04 (A) - Không có quyền xem lớp → HTTP 403")]
    public async Task GetList_UnauthorizedScope_ReturnsForbid()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<GetSwimClassesQuery>(), CancellationToken.None))
            .ThrowsAsync(new UnauthorizedAccessException());

        Assert.IsType<ForbidResult>(await Controller(sender).GetList(new GetSwimClassesQuery { Scope = "COACH" }, CancellationToken.None));
    }

    [Fact(DisplayName = "UTCID05 (A) - Chi tiết lớp không tìm thấy → HTTP 404")]
    public async Task GetById_MissingClass_ReturnsNotFound()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<GetSwimClassByIdQuery>(), CancellationToken.None))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy lớp dạy bơi."));

        var result = await Controller(sender).GetById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact(DisplayName = "UTCID06 (A) - Thiếu user ID trong claim → HTTP 403, không gửi query")]
    public async Task GetList_MissingUserId_ReturnsForbidBeforeSend()
    {
        var sender = new Mock<ISender>();

        var result = await Controller(sender, includeUserId: false).GetList(new GetSwimClassesQuery(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        sender.VerifyNoOtherCalls();
    }

    private static SwimClassesController Controller(Mock<ISender> sender, bool includeUserId = true)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, "CUSTOMER") };
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

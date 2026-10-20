using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffScheduleControllerAuthorizationTests
{
    [Fact]
    public async Task GivenAdmin_WhenReadingAllSchedule_ThenQueryIsDispatched()
    {
        var sender = CreateSender();
        var controller = CreateController(sender.Object, "ADMIN");

        var result = await controller.GetSchedule(new GetScheduleQuery(), CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        sender.Verify(item => item.Send(It.IsAny<GetScheduleQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenSales_WhenReadingAllSchedule_ThenRequestIsForbiddenWithoutDispatch()
    {
        var sender = CreateSender();
        var controller = CreateController(sender.Object, "SALES");

        var result = await controller.GetSchedule(new GetScheduleQuery(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        sender.Verify(item => item.Send(It.IsAny<GetScheduleQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<ISender> CreateSender()
    {
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(It.IsAny<GetScheduleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<GetScheduleResponse>());
        return sender;
    }

    private static StaffsController CreateController(ISender sender, string role)
    {
        var userId = Guid.NewGuid();
        return new StaffsController(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "unit-test"))
                }
            }
        };
    }
}

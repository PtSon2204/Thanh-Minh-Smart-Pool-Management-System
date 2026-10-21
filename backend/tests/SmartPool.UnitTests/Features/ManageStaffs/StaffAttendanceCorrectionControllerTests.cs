using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Features.ManageStaffs.Queries.GetAttendanceHistory;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffAttendanceCorrectionControllerTests
{
    [Fact]
    public async Task GivenSales_WhenCorrectingAttendance_ThenForbidWithoutDispatch()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, "SALES", Guid.NewGuid());

        var result = await controller.CorrectAttendance(Guid.NewGuid(), new ManualAttendanceCorrectionCommand(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        sender.Verify(item => item.Send(It.IsAny<ManualAttendanceCorrectionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAdmin_WhenCorrectingAttendance_ThenRouteAndClaimOverrideRequestIdentity()
    {
        var routeId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        ManualAttendanceCorrectionCommand? captured = null;
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(It.IsAny<ManualAttendanceCorrectionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<RecordAttendanceResponse>, CancellationToken>((request, _) => captured = (ManualAttendanceCorrectionCommand)request)
            .ReturnsAsync(new RecordAttendanceResponse());
        var controller = CreateController(sender.Object, "ADMIN", callerId);

        var result = await controller.CorrectAttendance(routeId, new ManualAttendanceCorrectionCommand
        {
            AssignmentId = Guid.NewGuid(),
            PerformedByUserId = Guid.NewGuid()
        }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        Assert.NotNull(captured);
        Assert.Equal(routeId, captured.AssignmentId);
        Assert.Equal(callerId, captured.PerformedByUserId);
    }

    [Fact]
    public async Task GivenSales_WhenReadingAttendanceHistory_ThenForbidWithoutDispatch()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, "SALES", Guid.NewGuid());

        var result = await controller.GetAttendanceHistory(Guid.NewGuid(), new GetAttendanceHistoryQuery(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        sender.Verify(item => item.Send(It.IsAny<GetAttendanceHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAdmin_WhenReadingAttendanceHistory_ThenRouteAssignmentOverridesQuery()
    {
        var routeId = Guid.NewGuid();
        GetAttendanceHistoryQuery? captured = null;
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(It.IsAny<GetAttendanceHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PagedResponse<GetAttendanceHistoryResponse>>, CancellationToken>((request, _) => captured = (GetAttendanceHistoryQuery)request)
            .ReturnsAsync(new PagedResponse<GetAttendanceHistoryResponse>());
        var controller = CreateController(sender.Object, "ADMIN", Guid.NewGuid());

        var result = await controller.GetAttendanceHistory(routeId, new GetAttendanceHistoryQuery { AssignmentId = Guid.NewGuid() }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        Assert.NotNull(captured);
        Assert.Equal(routeId, captured.AssignmentId);
    }

    private static StaffsController CreateController(ISender sender, string role, Guid userId) => new(sender)
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

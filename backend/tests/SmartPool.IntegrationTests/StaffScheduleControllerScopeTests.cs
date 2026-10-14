using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Contracts;
using SmartPool.API.Controllers;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;

namespace SmartPool.IntegrationTests;

public sealed class StaffScheduleControllerScopeTests
{
    [Fact]
    public async Task GivenSelfScheduleRequest_WhenReadingMyExactDateSchedule_ThenEmployeeIdCannotBindAndClaimIdIsSent()
    {
        var claimedEmployeeId = Guid.NewGuid();
        var sender = new CapturingSender();
        var controller = new StaffsController(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, claimedEmployeeId.ToString())], "test"))
                }
            }
        };

        var result = await controller.GetMySchedule(new GetMyScheduleRequest
        {
            Date = new DateOnly(2026, 10, 6),
            PageIndex = 2,
            PageSize = 7
        }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        Assert.Null(typeof(GetMyScheduleRequest).GetProperty("EmployeeId"));
        Assert.NotNull(sender.ScheduleQuery);
        Assert.Equal(claimedEmployeeId, sender.ScheduleQuery.EmployeeId);
        Assert.True(sender.ScheduleQuery.RequireEmployee);
        Assert.Equal(new DateOnly(2026, 10, 6), sender.ScheduleQuery.Date);
        Assert.Equal(2, sender.ScheduleQuery.PageIndex);
        Assert.Equal(7, sender.ScheduleQuery.PageSize);
    }

    [Fact]
    public async Task GivenMyDateRange_WhenReadingSchedule_ThenClaimEmployeeIdAndRangeAreSent()
    {
        var claimedEmployeeId = Guid.NewGuid();
        var sender = new CapturingSender();
        var controller = new StaffsController(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, claimedEmployeeId.ToString())], "test"))
                }
            }
        };
        var fromDate = new DateOnly(2026, 10, 6);
        var toDate = fromDate.AddDays(8);

        var result = await controller.GetMySchedule(new GetMyScheduleRequest
        {
            FromDate = fromDate,
            ToDate = toDate
        }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        Assert.NotNull(sender.ScheduleQuery);
        Assert.Equal(claimedEmployeeId, sender.ScheduleQuery.EmployeeId);
        Assert.True(sender.ScheduleQuery.RequireEmployee);
        Assert.Equal(fromDate, sender.ScheduleQuery.FromDate);
        Assert.Equal(toDate, sender.ScheduleQuery.ToDate);
        Assert.Null(sender.ScheduleQuery.Date);
    }

    private sealed class CapturingSender : ISender
    {
        public GetScheduleQuery? ScheduleQuery { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetScheduleQuery scheduleQuery)
            {
                ScheduleQuery = scheduleQuery;
                return Task.FromResult((TResponse)(object)new PagedResponse<GetScheduleResponse>());
            }

            throw new InvalidOperationException("Unexpected request type.");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            throw new InvalidOperationException("Unexpected request type.");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Unexpected request type.");
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Unexpected stream request.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Unexpected stream request.");
        }
    }
}

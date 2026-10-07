using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageReports.Queries.GetCustomerStatistics;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ISender _sender;

    public ReportsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy dữ liệu báo cáo thống kê khách hàng và hoạt động sử dụng dịch vụ tại bể bơi.
    /// </summary>
    /// <param name="query">Các tiêu chí lọc (period: today, 7days, 30days, month, custom...)</param>
    /// <param name="cancellationToken"></param>
    [HttpGet("customer-statistics")]
    [ProducesResponseType(typeof(CustomerStatisticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerStatistics([FromQuery] GetCustomerStatisticsQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }
}

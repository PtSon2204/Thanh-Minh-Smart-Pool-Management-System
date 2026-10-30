using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.ManageReports.Queries.GetCustomerStatistics;
using SmartPool.Application.Features.ManageReports.Queries.GetTicketRevenueStatistics;
using SmartPool.Application.Features.ManageReports.Queries.GetMonthlyTicketStatistics;
using SmartPool.Application.Features.ManageReports.Queries.GetIncidentStatistics;

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
    [RequirePermission("reports", "read")]
    [ProducesResponseType(typeof(CustomerStatisticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerStatistics([FromQuery] GetCustomerStatisticsQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy dữ liệu thống kê doanh thu bán vé theo loại vé và thời gian.
    /// </summary>
    /// <param name="query">Các tiêu chí lọc (period: today, 7days, 30days, month, custom...)</param>
    /// <param name="cancellationToken"></param>
    [HttpGet("ticket-revenue")]
    [RequirePermission("reports", "read")]
    [ProducesResponseType(typeof(TicketRevenueStatisticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTicketRevenueStatistics([FromQuery] GetTicketRevenueStatisticsQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy dữ liệu báo cáo thống kê vé tháng, hội viên và danh sách cảnh báo hạn thẻ.
    /// </summary>
    /// <param name="query">Bộ lọc trạng thái thẻ (all, active, expiring_soon, expired)</param>
    /// <param name="cancellationToken"></param>
    [HttpGet("monthly-tickets")]
    [RequirePermission("reports", "read")]
    [ProducesResponseType(typeof(MonthlyTicketStatisticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlyTicketStatistics([FromQuery] GetMonthlyTicketStatisticsQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy dữ liệu báo cáo thống kê sự cố vận hành và an toàn bể bơi.
    /// </summary>
    /// <param name="query">Bộ lọc sự cố (all, critical_urgent, in_progress)</param>
    /// <param name="cancellationToken"></param>
    [HttpGet("incidents")]
    [RequirePermission("reports", "read")]
    [ProducesResponseType(typeof(IncidentStatisticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidentStatistics([FromQuery] GetIncidentStatisticsQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }
}

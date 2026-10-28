using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Commands.CreateWaterQualityLog;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityAnalytics;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogById;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogs;
using SmartPool.Application.Features.ManageWaterQuality.Queries.PreviewWaterQualityEvaluation;
using SmartPool.Application.Features.ManageWaterQuality.Services;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/v1/water-quality-logs")]
[Authorize]
public sealed class WaterQualityLogsController : ControllerBase
{
    private readonly ISender _sender;

    public WaterQualityLogsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy danh sách lịch sử kiểm tra chất lượng nước có phân trang và lọc theo ngày, trạng thái, người đo.
    /// </summary>
    [HttpGet]
    [RequirePermission("water_quality", "read")]
    [ProducesResponseType(typeof(PagedResponse<WaterQualityLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs([FromQuery] GetWaterQualityLogsQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy dữ liệu phân tích, thống kê chất lượng nước và biểu đồ xu hướng.
    /// </summary>
    [HttpGet("analytics")]
    [RequirePermission("water_quality", "read")]
    [ProducesResponseType(typeof(WaterQualityAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalytics([FromQuery] GetWaterQualityAnalyticsQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xem trước kết quả đánh giá và gợi ý hóa chất theo chỉ số nước đưa vào (dùng cho form nhập liệu xem trước tức thì).
    /// </summary>
    [HttpPost("preview")]
    [RequirePermission("water_quality", "read")]
    [ProducesResponseType(typeof(WaterQualityEvaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewEvaluation([FromBody] PreviewWaterQualityEvaluationQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Chỉ số nhập liệu không hợp lệ.",
                errors = ex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }
    }

    /// <summary>
    /// Xem chi tiết một phiếu kiểm tra chất lượng nước theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("water_quality", "read")]
    [ProducesResponseType(typeof(WaterQualityLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetWaterQualityLogByIdQuery(id), cancellationToken);
        if (result is null)
        {
            return NotFound(new { message = $"Không tìm thấy phiếu kiểm tra chất lượng nước có mã '{id}'." });
        }
        return Ok(result);
    }

    /// <summary>
    /// Tạo mới một phiếu kiểm tra chất lượng nước.
    /// Hệ thống sẽ tự động đối chiếu ngưỡng chuẩn, đánh giá trạng thái và đưa ra khuyến nghị xử lý hóa chất.
    /// </summary>
    [HttpPost]
    [RequirePermission("water_quality", "create")]
    [ProducesResponseType(typeof(WaterQualityLogDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateLog([FromBody] CreateWaterQualityLogCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Dữ liệu đo không hợp lệ.",
                errors = ex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}

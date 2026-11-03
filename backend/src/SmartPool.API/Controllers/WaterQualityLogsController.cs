using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;
using SmartPool.Application.Features.ManageWaterQuality.Commands.CreateWaterQualityLog;
using SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutyOptions;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutySchedules;
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
    /// Xem danh sách lịch trực bể (kiểm tra chất lượng nước và giám sát bể) theo bộ lọc ngày, ca, nhân viên.
    /// </summary>
    [HttpGet("duty-schedules")]
    [RequirePermission("water_quality", "read")]
    [ProducesResponseType(typeof(PagedResponse<PoolDutyScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDutySchedules([FromQuery] GetPoolDutySchedulesQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách nhân viên trực bể và danh sách ca trực đang hoạt động.
    /// </summary>
    [HttpGet("duty-schedules/options")]
    [RequirePermission("water_quality", "read")]
    [ProducesResponseType(typeof(PoolDutyOptionsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDutyScheduleOptions(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPoolDutyOptionsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Tự động gán lịch trực bể theo khoảng ngày, cân bằng số ca trực và ưu tiên nhân viên có lịch làm việc.
    /// </summary>
    [HttpPost("duty-schedules/auto-assign")]
    [RequirePermission("water_quality", "create")]
    [ProducesResponseType(typeof(AutoAssignPoolDutyResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AutoAssignDutySchedules([FromBody] AutoAssignPoolDutyCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Thông tin tự động phân công lịch trực bể không hợp lệ.",
                errors = ex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Tạo hoặc gán lịch trực bể thủ công cho một ngày và ca trực.
    /// </summary>
    [HttpPost("duty-schedules")]
    [RequirePermission("water_quality", "create")]
    [ProducesResponseType(typeof(PoolDutyScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveDutySchedule([FromBody] UpdatePoolDutyScheduleCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Thông tin lịch trực bể không hợp lệ.",
                errors = ex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật lịch trực bể (đổi nhân viên trực, cập nhật trạng thái trực bể, ghi chú).
    /// </summary>
    [HttpPut("duty-schedules/{id:guid}")]
    [RequirePermission("water_quality", "update")]
    [ProducesResponseType(typeof(PoolDutyScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDutySchedule([FromRoute] Guid id, [FromBody] UpdatePoolDutyScheduleCommand command, CancellationToken cancellationToken)
    {
        try
        {
            command.Id = id;
            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Thông tin cập nhật lịch trực bể không hợp lệ.",
                errors = ex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
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

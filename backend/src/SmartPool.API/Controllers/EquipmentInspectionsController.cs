using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspectionForm;
using SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspections;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/equipment-inspections")]
[Authorize]
public sealed class EquipmentInspectionsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<EquipmentInspectionsController> _logger;

    public EquipmentInspectionsController(ISender sender, ILogger<EquipmentInspectionsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách các thiết bị cần kiểm tra vận hành / an toàn trước khi vào ca làm kèm thông tin trạng thái các ca trực hôm nay.
    /// </summary>
    [HttpGet("form")]
    [ProducesResponseType(typeof(DailyInspectionFormResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInspectionForm(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetDailyInspectionFormQuery(), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Lỗi lấy form kiểm tra thiết bị đầu ca.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không thể lấy danh mục thiết bị và thông tin ca trực. Vui lòng thử lại." });
        }
    }

    /// <summary>
    /// Nộp kết quả kiểm tra thiết bị đầu ca (Checklist). 
    /// Nếu có thiết bị hỏng, hệ thống tự động khóa thiết bị sang trạng thái bảo trì và lập lịch khắc phục.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DailyInspectionDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitInspection([FromBody] SubmitDailyInspectionCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var inspectorId))
            return Unauthorized(new { message = "Không xác định được danh tính nhân viên." });

        command.InspectorId = inspectorId;

        try
        {
            var result = await _sender.Send(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Thông tin kiểm tra thiết bị không hợp lệ.",
                errors = ex.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Lỗi xử lý kiểm tra thiết bị đầu ca.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không thể ghi nhận kiểm tra thiết bị. Vui lòng thử lại." });
        }
    }

    /// <summary>
    /// Lấy lịch sử các lượt kiểm tra thiết bị có phân trang và lọc theo ngày/trạng thái.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DailyInspectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInspections([FromQuery] GetDailyInspectionsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Lỗi lấy lịch sử kiểm tra thiết bị.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không thể lấy lịch sử kiểm tra thiết bị. Vui lòng thử lại." });
        }
    }
}

using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Inspections.Commands.ResolveInspectionItem;
using SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspectionForm;
using SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspections;
using SmartPool.Application.Interfaces.Services;

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

    /// <summary>
    /// Upload ảnh hiện trường hư hỏng hoặc ảnh minh chứng sau sửa chữa lên Cloudinary.
    /// </summary>
    [HttpPost("images")]
    [Authorize]
    [RequestSizeLimit(15 * 1024 * 1024)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadImage(
        IFormFile? file,
        [FromServices] IStorageService storageService,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn tệp hình ảnh hợp lệ." });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Chỉ chấp nhận định dạng ảnh JPG, JPEG, PNG, WEBP." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "Kích thước ảnh không được vượt quá 10MB." });

        try
        {
            using var stream = file.OpenReadStream();
            var imageUrl = await storageService.UploadAsync(stream, file.FileName, file.ContentType, cancellationToken);
            return Ok(new { url = imageUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tải ảnh kiểm tra thiết bị lên Cloudinary.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không thể tải ảnh lên hệ thống lưu trữ. Vui lòng thử lại." });
        }
    }

    public sealed record ResolveItemRequest(string Action, string? Note, List<string>? ResolutionImageUrls);

    /// <summary>
    /// Quản trị viên (Admin) xác nhận đã sửa chữa hoặc thay thế thiết bị gặp sự cố trong biên bản kiểm tra.
    /// Thiết bị sẽ tự động được đưa về trạng thái Hoạt động (Active).
    /// </summary>
    [HttpPatch("{inspectionId:guid}/items/{equipmentId:guid}/resolve")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(DailyInspectionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveInspectionItem(
        Guid inspectionId,
        Guid equipmentId,
        [FromBody] ResolveItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Unauthorized(new { message = "Không xác định được danh tính quản trị viên." });

        var command = new ResolveInspectionItemCommand
        {
            InspectionId = inspectionId,
            EquipmentId = equipmentId,
            AdminId = adminId,
            Action = request.Action,
            Note = request.Note,
            ResolutionImageUrls = request.ResolutionImageUrls ?? []
        };

        try
        {
            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = ex.Errors.FirstOrDefault()?.ErrorMessage ?? ex.Message,
                errors = ex.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Lỗi xác nhận xử lý thiết bị sự cố.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không thể cập nhật trạng thái xử lý thiết bị. Vui lòng thử lại." });
        }
    }
}

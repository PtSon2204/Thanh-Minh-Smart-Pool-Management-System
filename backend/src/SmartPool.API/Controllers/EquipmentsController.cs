using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;
using SmartPool.Application.Features.ManageEquipments.Commands.UpdateEquipment;
using SmartPool.Application.Features.ManageEquipments.Commands.SetEquipmentStatus;
using SmartPool.Application.Features.ManageEquipments.Commands.OpenCameraView;
using SmartPool.Application.Features.ManageEquipments.Commands.CloseCameraView;
using SmartPool.Application.Features.ManageEquipments.DTOs;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipmentFilters;
using SmartPool.Application.Features.ManageEquipments.Queries.GetCameraViewConfig;
using SmartPool.Application.Features.ManageEquipments.Queries.GetCameraViewStatus;
using SmartPool.Application.Features.ManageEquipments.Queries.GetCameraStatus;
using SmartPool.Application.Features.ManageEquipments.Queries.GetCameraFrame;
using System.Security.Claims;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/equipments")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class EquipmentsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<EquipmentsController> _logger;

    public EquipmentsController(ISender sender, ILogger<EquipmentsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Camera View Endpoints
    // -------------------------------------------------------------------------

    [HttpGet("{equipmentId:guid}/camera/views")]
    public async Task<IActionResult> GetCameraViewConfiguration(Guid equipmentId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return await SendCameraAsync(new GetCameraViewConfigQuery(equipmentId), cancellationToken);
    }

    [HttpPost("{equipmentId:guid}/camera/views")]
    [RequestSizeLimit(73728)]
    public async Task<IActionResult> OpenCameraView(Guid equipmentId, [FromBody] CameraOffer offer, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return await SendCameraAsync(new OpenCameraViewCommand(equipmentId, offer, GetUserId()), cancellationToken);
    }

    [HttpGet("{equipmentId:guid}/camera/views/{viewId:guid}")]
    public async Task<IActionResult> ReadCameraView(Guid equipmentId, Guid viewId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return await SendCameraAsync(new GetCameraViewStatusQuery(equipmentId, viewId, GetUserId()), cancellationToken);
    }

    [HttpDelete("{equipmentId:guid}/camera/views/{viewId:guid}")]
    public async Task<IActionResult> CloseCameraView(Guid equipmentId, Guid viewId, CancellationToken cancellationToken)
    {
        await _sender.Send(new CloseCameraViewCommand(equipmentId, viewId, GetUserId()), cancellationToken);
        return NoContent();
    }

    [HttpGet("{equipmentId:guid}/camera")]
    public async Task<IActionResult> GetCameraStatus(Guid equipmentId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return await SendCameraAsync(new GetCameraStatusQuery(equipmentId), cancellationToken);
    }

    [HttpGet("{equipmentId:guid}/camera/frame")]
    public async Task<IActionResult> GetCameraFrame(Guid equipmentId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var frame = await _sender.Send(new GetCameraFrameQuery(equipmentId), cancellationToken);
            return frame == null
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Camera không có frame hợp lệ hoặc đã mất kết nối." })
                : File(frame, "image/jpeg");
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // -------------------------------------------------------------------------
    // Equipment CRUD Endpoints
    // -------------------------------------------------------------------------

    [HttpPost]
    [ProducesResponseType(typeof(EquipmentListItem), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEquipmentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetList), new { searchTerm = result.Name }, result);
        }
        catch (ValidationException exception)
        {
            return BadRequest(ValidationError(exception));
        }
        catch (CameraConnectionException exception)
        {
            var status = exception.Code switch
            {
                "Duplicate" => StatusCodes.Status409Conflict,
                "AddressNotAllowed" or "InvalidConfiguration" => StatusCodes.Status400BadRequest,
                "NotConfigured" or "Capacity" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status422UnprocessableEntity
            };
            return StatusCode(status, new { message = exception.Message, code = exception.Code });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Camera library exceptions and database JSON parameters can contain protected configuration.
            if (CameraMetadata.IsCamera(command.Category)) _logger.LogError("Unable to create camera equipment; error type {ErrorType}.", exception.GetType().Name);
            else _logger.LogError(exception, "Unable to create equipment.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Thêm thiết bị thất bại. Vui lòng thử lại." });
        }
    }

    [HttpPut("{equipmentId:guid}")]
    public async Task<IActionResult> Update(Guid equipmentId, [FromBody] UpdateEquipmentCommand command, CancellationToken cancellationToken)
    {
        command.Id = equipmentId;
        try
        {
            return await _sender.Send(command, cancellationToken) ? NoContent() : NotFound(new { message = "Không tìm thấy thiết bị." });
        }
        catch (ValidationException exception) { return BadRequest(ValidationError(exception)); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError("Unable to update equipment; error type {ErrorType}.", exception.GetType().Name);
            return StatusCode(500, new { message = "Cập nhật thiết bị thất bại. Vui lòng thử lại." });
        }
    }

    [HttpPatch("{equipmentId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid equipmentId, [FromBody] SetEquipmentStatusCommand command, CancellationToken cancellationToken)
    {
        command.Id = equipmentId;
        try
        {
            return await _sender.Send(command, cancellationToken) ? NoContent() : NotFound(new { message = "Không tìm thấy thiết bị." });
        }
        catch (ValidationException exception) { return BadRequest(ValidationError(exception)); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError("Unable to change equipment status; error type {ErrorType}.", exception.GetType().Name);
            return StatusCode(500, new { message = "Thay đổi trạng thái thiết bị thất bại. Vui lòng thử lại." });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<EquipmentListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] GetEquipmentsQuery query, CancellationToken cancellationToken)
    {
        try { return Ok(await _sender.Send(query, cancellationToken)); }
        catch (ValidationException exception) { return BadRequest(ValidationError(exception)); }
    }

    [HttpGet("filters")]
    [ProducesResponseType(typeof(EquipmentFilterOptions), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilters(CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new GetEquipmentFiltersQuery(), cancellationToken));

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Shared handler for camera endpoints — maps CameraConnectionException codes to HTTP status.</summary>
    private async Task<IActionResult> SendCameraAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        try { return Ok(await _sender.Send(request, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (CameraConnectionException ex)
        {
            var status = ex.Code switch
            {
                "InvalidOffer" => StatusCodes.Status400BadRequest,
                "ViewEnded" => StatusCodes.Status410Gone,
                "Duplicate" => StatusCodes.Status409Conflict,
                "AddressNotAllowed" or "InvalidConfiguration" => StatusCodes.Status400BadRequest,
                "NotConfigured" or "Capacity" or "Unavailable" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status503ServiceUnavailable
            };
            return StatusCode(status, new { message = ex.Message, code = ex.Code });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError("Camera operation failed; error type {ErrorType}.", ex.GetType().Name);
            return StatusCode(500, new { message = "Thao tác thất bại. Vui lòng thử lại." });
        }
    }

    private static object ValidationError(ValidationException exception) => new
    {
        message = "Dữ liệu không hợp lệ.",
        errors = exception.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).ToArray())
    };
}

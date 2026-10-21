using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;
using SmartPool.Application.Features.ManageEquipments.Commands.UpdateEquipment;
using SmartPool.Application.Features.ManageEquipments.Commands.SetEquipmentStatus;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Domain.Entities;
using System.Security.Claims;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/equipments")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class EquipmentsController(ISender sender, ILogger<EquipmentsController> logger) : ControllerBase
{
    [HttpGet("{equipmentId:guid}/camera/views")]
    public async Task<IActionResult> GetCameraViewConfiguration(Guid equipmentId, [FromServices] IRepository<Equipment> repository,
        [FromServices] ICameraStreamService streams, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(equipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true) return NotFound(new { message = "Không tìm thấy camera" });
        if (!CameraMetadata.IsCamera(equipment.Category)) return BadRequest(new { message = "Thiết bị không thuộc nhóm Camera." });
        if (equipment.Status != "Active") return StatusCode(503, new { message = "Camera hiện không khả dụng" });
        Response.Headers.CacheControl = "no-store";
        return Ok(streams.GetConfiguration());
    }

    [HttpPost("{equipmentId:guid}/camera/views")]
    [RequestSizeLimit(73728)]
    public async Task<IActionResult> OpenCameraView(Guid equipmentId, [FromBody] CameraOffer offer, [FromServices] IRepository<Equipment> repository,
        [FromServices] ICameraStreamService streams, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(equipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true) return NotFound(new { message = "Không tìm thấy camera" });
        if (!CameraMetadata.IsCamera(equipment.Category)) return BadRequest(new { message = "Thiết bị không thuộc nhóm Camera." });
        if (equipment.Status != "Active") return StatusCode(503, new { message = "Camera hiện không khả dụng" });
        Response.Headers.CacheControl = "no-store";
        try
        {
            return Ok(await streams.OpenAsync(equipmentId, User.FindFirstValue(ClaimTypes.NameIdentifier)!, offer, cancellationToken));
        }
        catch (CameraConnectionException exception) { return StatusCode(exception.Code == "InvalidOffer" ? 400 : 503, new { message = exception.Message, code = exception.Code }); }
    }

    [HttpGet("{equipmentId:guid}/camera/views/{viewId:guid}")]
    public async Task<IActionResult> ReadCameraView(Guid equipmentId, Guid viewId,
        [FromServices] IRepository<Equipment> repository, [FromServices] ICameraStreamService streams, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(equipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true) return NotFound(new { message = "Không tìm thấy camera" });
        if (!CameraMetadata.IsCamera(equipment.Category)) return BadRequest(new { message = "Thiết bị không thuộc nhóm Camera." });
        if (equipment.Status != "Active") return StatusCode(503, new { message = "Camera hiện không khả dụng" });
        Response.Headers.CacheControl = "no-store";
        try
        {
            return Ok(await streams.GetStatusAsync(equipmentId, viewId, User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken));
        }
        catch (CameraConnectionException exception)
        {
            return StatusCode(exception.Code == "ViewEnded" ? 410 : 503, new { message = exception.Message, code = exception.Code });
        }
    }

    [HttpDelete("{equipmentId:guid}/camera/views/{viewId:guid}")]
    public async Task<IActionResult> CloseCameraView(Guid equipmentId, Guid viewId, [FromServices] ICameraStreamService streams)
    {
        await streams.CloseAsync(equipmentId, viewId, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return NoContent();
    }

    [HttpGet("{equipmentId:guid}/camera")]
    public async Task<IActionResult> GetCameraStatus(Guid equipmentId, [FromServices] IRepository<Equipment> repository,
        [FromServices] ICameraService cameras, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(equipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true || !CameraMetadata.IsCamera(equipment.Category)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Ok(cameras.GetStatus(equipmentId));
    }

    [HttpGet("{equipmentId:guid}/camera/frame")]
    public async Task<IActionResult> GetCameraFrame(Guid equipmentId, [FromServices] IRepository<Equipment> repository,
        [FromServices] ICameraService cameras, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(equipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true || !CameraMetadata.IsCamera(equipment.Category)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var frame = equipment.Status == "Active" ? cameras.GetFrame(equipmentId) : null;
        return frame == null ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Camera không có frame hợp lệ hoặc đã mất kết nối." }) : File(frame, "image/jpeg");
    }

    [HttpPost]
    [ProducesResponseType(typeof(EquipmentListItem), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEquipmentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(command, cancellationToken);
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
            if (CameraMetadata.IsCamera(command.Category)) logger.LogError("Unable to create camera equipment; error type {ErrorType}.", exception.GetType().Name);
            else logger.LogError(exception, "Unable to create equipment.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Thêm thiết bị thất bại. Vui lòng thử lại." });
        }
    }

    [HttpPut("{equipmentId:guid}")]
    public async Task<IActionResult> Update(Guid equipmentId, [FromBody] UpdateEquipmentCommand command, CancellationToken cancellationToken)
    {
        command.Id = equipmentId;
        try
        {
            return await sender.Send(command, cancellationToken) ? NoContent() : NotFound(new { message = "Không tìm thấy thiết bị." });
        }
        catch (ValidationException exception) { return BadRequest(ValidationError(exception)); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("Unable to update equipment; error type {ErrorType}.", exception.GetType().Name);
            return StatusCode(500, new { message = "Cập nhật thiết bị thất bại. Vui lòng thử lại." });
        }
    }

    [HttpPatch("{equipmentId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid equipmentId, [FromBody] SetEquipmentStatusCommand command, CancellationToken cancellationToken)
    {
        command.Id = equipmentId;
        try
        {
            return await sender.Send(command, cancellationToken) ? NoContent() : NotFound(new { message = "Không tìm thấy thiết bị." });
        }
        catch (ValidationException exception) { return BadRequest(ValidationError(exception)); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("Unable to change equipment status; error type {ErrorType}.", exception.GetType().Name);
            return StatusCode(500, new { message = "Thay đổi trạng thái thiết bị thất bại. Vui lòng thử lại." });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<EquipmentListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] GetEquipmentsQuery query, CancellationToken cancellationToken)
    {
        try { return Ok(await sender.Send(query, cancellationToken)); }
        catch (ValidationException exception)
        {
            return BadRequest(ValidationError(exception));
        }
    }

    [HttpGet("filters")]
    [ProducesResponseType(typeof(EquipmentFilterOptions), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilters(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetEquipmentFiltersQuery(), cancellationToken));

    private static object ValidationError(ValidationException exception) => new
    {
        message = "Dữ liệu không hợp lệ.",
        errors = exception.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).ToArray())
    };
}

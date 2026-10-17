using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Domain.Entities;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/equipments")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class EquipmentsController(ISender sender, ILogger<EquipmentsController> logger) : ControllerBase
{
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

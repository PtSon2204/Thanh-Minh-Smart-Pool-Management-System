using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.Notifications.Commands.CreateNotification;
using SmartPool.Application.Features.Notifications.Commands.MarkNotificationAsRead;
using SmartPool.Application.Features.Notifications.Queries.GetNotifications;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Lấy danh sách thông báo dành cho Admin (có phân trang, lọc theo type, isRead).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(GetNotificationsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications([FromQuery] GetNotificationsQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Đánh dấu một thông báo là đã đọc.</summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new MarkNotificationAsReadCommand(id), cancellationToken);
            return Ok(new { success = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Tạo mới một thông báo (dùng nội bộ hoặc test).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationCommand command, CancellationToken cancellationToken)
    {
        var id = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetNotifications), new { id }, new { id });
    }
}

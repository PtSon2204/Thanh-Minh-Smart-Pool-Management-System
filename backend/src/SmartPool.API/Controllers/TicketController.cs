using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartPool.API.Extensions;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/tickets")]
    public class TicketController : ControllerBase
    {
        private readonly ISender _sender;

        public TicketController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Bán vé trực tiếp tại quầy (Tiền mặt).</summary>
        [HttpPost("sell-offline")]
        [RequirePermission("ticket_sales", "create")]
        [ProducesResponseType(typeof(SellOfflineTicketResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SellOffline([FromBody] SellOfflineTicketCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sender.Send(command, cancellationToken);
                return Ok(result);
            }   
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Tạo đơn bán vé tại quầy (Chờ chuyển khoản).</summary>
        [HttpPost("create-pending-order")]
        [RequirePermission("ticket_sales", "create")]
        [ProducesResponseType(typeof(SmartPool.Application.Features.ManageTickets.Ticket.Commands.CreatePendingOrder.CreatePendingOrderResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CreatePendingOrder([FromBody] SmartPool.Application.Features.ManageTickets.Ticket.Commands.CreatePendingOrder.CreatePendingOrderCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sender.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Hàm này sẽ được fe gọi 3s 1 lần để kiểm tra xem trạng thái thanh toán đã đổi chưa.</summary>
        [HttpGet("orders/{orderId}/status")]
        [RequirePermission("ticket_sales", "read")]
        [ProducesResponseType(typeof(SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetOrderStatus.GetOrderStatusResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrderStatus(Guid orderId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sender.Send(new SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetOrderStatus.GetOrderStatusQuery { OrderId = orderId }, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Khách hàng kiểm tra đơn thuộc tài khoản của mình.</summary>
        [HttpGet("me/orders/{orderId:guid}/status")]
        [RequirePermission("customer_tickets", "read")]
        public async Task<IActionResult> GetMyOrderStatus(Guid orderId, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId)) return Forbid();
            try
            {
                var result = await _sender.Send(new SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetOrderStatus.GetOrderStatusQuery
                {
                    OrderId = orderId,
                    UserId = userId
                }, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Không tìm thấy đơn hàng." });
            }
        }

        /// <summary>Thanh toán và tạo vé online (Cho Customer).</summary>
        [HttpPost("checkout")]
        [RequirePermission("customer_tickets", "create")]
        [ProducesResponseType(typeof(SmartPool.Application.Features.ManageTickets.Ticket.Commands.CheckoutOnline.CheckoutOnlineResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckoutOnline([FromBody] SmartPool.Application.Features.ManageTickets.Ticket.Commands.CheckoutOnline.CheckoutOnlineCommand command, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId)) return Forbid();
            command.UserId = userId;
            try
            {
                var result = await _sender.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Lấy danh sách vé đã mua của tôi.</summary>
        [HttpGet("me")]
        [RequirePermission("customer_tickets", "read")]
        public async Task<IActionResult> GetMyTickets(CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId)) return Forbid();
            var query = new SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyTickets.GetMyTicketsQuery { UserId = userId };
            try
            {
                var result = await _sender.Send(query, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Cập nhật mã ngắn (6 chữ số) của vé — chỉ chủ vé được phép.</summary>
        [HttpPut("{ticketId}/code")]
        [RequirePermission("customer_tickets", "update")]
        [ProducesResponseType(typeof(SmartPool.Application.Features.ManageTickets.Ticket.Commands.UpdateTicketCode.UpdateTicketCodeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateTicketCode(Guid ticketId, [FromBody] UpdateTicketCodeRequest body, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId)) return Forbid();

            var command = new SmartPool.Application.Features.ManageTickets.Ticket.Commands.UpdateTicketCode.UpdateTicketCodeCommand
            {
                TicketId = ticketId,
                UserId = userId,
                Code = body.Code
            };

            try
            {
                var result = await _sender.Send(command, cancellationToken);
                if (!result.Success)
                    return BadRequest(new { message = result.Message });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    /// <summary>Body request cho endpoint cập nhật mã vé.</summary>
    public record UpdateTicketCodeRequest(string Code);
}

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartPool.API.Extensions;
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

        /// <summary>Thanh toán và tạo vé online (Cho Customer).</summary>
        [HttpPost("checkout")]
        [Authorize(Roles = "CUSTOMER")]
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
        [Authorize(Roles = "CUSTOMER")]
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
    }
}


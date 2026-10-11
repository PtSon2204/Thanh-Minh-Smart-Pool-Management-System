using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SePayController : ControllerBase
    {
        private readonly ISender _sender;

        public SePayController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook([FromBody] SePayWebhookPayload payload)
        {
            var content = payload.content ?? "";         
            
            var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? transactionRef = words.FirstOrDefault(w => w.StartsWith("SP", StringComparison.OrdinalIgnoreCase) && w.Length >= 8);

            if (transactionRef == null)
            {
                var index = content.IndexOf("SP", StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && content.Length >= index + 8)
                {
                    transactionRef = content.Substring(index, 8).ToUpper();
                }
            }

            if (string.IsNullOrEmpty(transactionRef))
            {
                return Ok(new { success = true, message = "No valid TransactionRef found. Ignored." });
            }

            var command = new ConfirmPendingOrderCommand 
            { 
                TransactionRef = transactionRef.ToUpper(),
                ActualAmount = payload.transferAmount,
                BankReferenceCode = payload.referenceCode
            };
            
            var result = await _sender.Send(command);

            if (!result.Success)
            {
                return Ok(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message });
        }
    }
}

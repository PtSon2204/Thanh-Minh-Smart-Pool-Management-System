using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder;
using SmartPool.API.Models;

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
            // SePay sends transfer details. We extract the content
            var content = payload.content ?? "";

            // Find SP123456 in the content
            // Assuming the transaction content has the TransactionRef somewhere
            // We can just query by passing the content and letting Handler find if there's a match?
            // Actually, SePay content might be "NGUYEN VAN A CHUYEN TIEN SP123456"
            // It's better if we extract words starting with "SP" and having 8 characters.
            
            var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? transactionRef = words.FirstOrDefault(w => w.StartsWith("SP", StringComparison.OrdinalIgnoreCase) && w.Length >= 8);

            if (transactionRef == null)
            {
                // Fallback: Just pass the whole content and let handler figure it out, 
                // but since our handler expects exact TransactionRef, we should extract it.
                // Let's just find the substring "SP" + 6 chars
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
                ActualAmount = payload.transferAmount
            };
            
            var result = await _sender.Send(command);

            if (!result.Success)
            {
                // Return 200 anyway so SePay doesn't retry endlessly for invalid codes
                return Ok(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message });
        }
    }
}

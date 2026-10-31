using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using System.Security.Cryptography;
using System.Text;

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

        [HttpPost("rental/webhook")]
        public async Task<IActionResult> RentalWebhook([FromBody] RentalSePayWebhookPayload payload,
            [FromServices] IServiceOperations rentals, [FromServices] IOptions<SePayOptions> options,
            CancellationToken cancellationToken = default)
        {
            var paymentOptions = options.Value;
            if (string.IsNullOrWhiteSpace(paymentOptions.ApiKey) || string.IsNullOrWhiteSpace(paymentOptions.AccountNumber))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "Payment verification is not configured." });
            var authorization = Request.Headers.Authorization.ToString();
            const string scheme = "Apikey ";
            if (!authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(authorization[scheme.Length..]), Encoding.UTF8.GetBytes(paymentOptions.ApiKey)))
                return Unauthorized(new { success = false, message = "Invalid webhook authentication." });
            if (payload.id <= 0 || !RentalPaymentPolicy.IsWholeVnd(payload.transferAmount)
                || string.IsNullOrWhiteSpace(payload.accountNumber) || payload.transferType is null)
                return BadRequest(new { success = false, message = "Invalid bank transaction metadata." });
            if (string.Equals(payload.transferType, "out", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(payload.accountNumber.Trim(), paymentOptions.AccountNumber.Trim(), StringComparison.Ordinal))
                return Ok(new { success = true, message = "Ignored non-collection event." });
            if (!string.Equals(payload.transferType, "in", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Invalid transfer direction." });
            var reference = RentalPaymentPolicy.ExtractReference(payload.code, payload.content) ?? string.Empty;
            if (!RentalPaymentPolicy.IsRentalReference(reference))
                return Ok(new { success = true, message = "Ignored non-rental payment reference." });
            var transfer = new BankTransferEvent(payload.id, payload.accountNumber.Trim(), payload.transferAmount, reference, payload.referenceCode);
            var result = await rentals.ApplyTransferAsync(transfer, cancellationToken);
            if (!result.Success)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = result.Message });
            return Ok(new { success = true, message = result.Message });
        }
    }
}

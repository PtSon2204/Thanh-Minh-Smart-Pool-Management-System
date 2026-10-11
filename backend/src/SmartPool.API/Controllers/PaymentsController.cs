using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManagePayments.DTOs;
using SmartPool.Application.Features.ManagePayments.Queries.GetAllPayments;
using SmartPool.Application.Features.ManagePayments.Queries.GetPaymentById;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public sealed class PaymentsController : ControllerBase
    {
        private readonly ISender _sender;

        public PaymentsController(ISender sender)
        {
            _sender = sender;
        }

        /// Lấy danh sách lịch sử giao dịch thanh toán
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<PaymentSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllPayments([FromQuery] GetAllPaymentsQuery query, CancellationToken cancellationToken)
        {
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

        /// Lấy thông tin chi tiết giao dịch thanh toán
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(PaymentDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPaymentById([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sender.Send(new GetPaymentByIdQuery { PaymentId = id }, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}

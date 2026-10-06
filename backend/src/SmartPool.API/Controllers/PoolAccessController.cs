using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry;
using SmartPool.Application.Features.AccessControlPool.Queries.GetDailyEntrySummary;
using SmartPool.Application.Features.AccessControlPool.Queries.GetEntryHistory;
using SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/pool-access")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
    public sealed class PoolAccessController : ControllerBase
    {
        private readonly ISender _sender;

        public PoolAccessController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Tra cứu thông tin và điều kiện vào bể của vé.</summary>
        [HttpPost("lookup")]
        [ProducesResponseType(typeof(LookupTicketResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Lookup([FromBody] LookupTicketQuery query, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Xác nhận một lượt vào bể.</summary>
        [HttpPost("entries")]
        [ProducesResponseType(typeof(ConfirmEntryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Confirm([FromBody] ConfirmEntryCommand command, CancellationToken cancellationToken)
        {
            var operatorId = GetOperatorId();
            if (operatorId is null)
            {
                return Unauthorized();
            }

            command.OperatorId = operatorId.Value;
            var result = await _sender.Send(command, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Lấy lịch sử lượt vào bể có phân trang.</summary>
        [HttpGet("entries")]
        [ProducesResponseType(typeof(SmartPool.Application.Common.Models.PagedResponse<GetEntryHistoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetHistory([FromQuery] GetEntryHistoryQuery query, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Lấy tổng số lượt vào bể đã được chấp nhận trong ngày.</summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(GetDailyEntrySummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSummary([FromQuery] GetDailyEntrySummaryQuery query, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return ToActionResult(result);
        }

        private Guid? GetOperatorId()
        {
            return User.TryGetUserId(out var operatorId)
                ? operatorId
                : null;
        }

        private ActionResult ToActionResult<T>(PoolAccessValidationResult<T> result)
        {
            return result.IsValid
                ? Ok(result.Value)
                : BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>(result.Errors!)));
        }
    }
}

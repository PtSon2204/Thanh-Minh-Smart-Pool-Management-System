using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.AdjustStock;
using SmartPool.Application.Features.ManageServices.Commands.CreateService;
using SmartPool.Application.Features.ManageServices.Commands.UpdateService;
using SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory;
using SmartPool.Application.Features.ManageServices.Queries.GetServices;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/services")]
    public sealed class ServicesController : ControllerBase
    {
        private readonly ISender _sender;

        public ServicesController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lấy danh sách dịch vụ có phân trang, tìm kiếm và lọc.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<GetServicesResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetServices([FromQuery] GetServicesQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Tạo mới dịch vụ.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreateServiceResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateService([FromBody] CreateServiceCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sender.Send(command, cancellationToken);
                return CreatedAtAction(nameof(GetServices), new { id = result.Id }, result);
            }
            catch (ValidationException exception)
            {
                return BadRequest(CreateValidationError(exception));
            }
        }

        /// <summary>Cập nhật thông tin dịch vụ, không thay đổi tồn kho.</summary>
        [HttpPut("{serviceId:guid}")]
        [ProducesResponseType(typeof(UpdateServiceResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateService(Guid serviceId, [FromBody] UpdateServiceCommand command, CancellationToken cancellationToken)
        {
            command.Id = serviceId;
            return await SendAsync(command, cancellationToken);
        }

        /// <summary>Điều chỉnh tồn kho dịch vụ.</summary>
        [HttpPost("{serviceId:guid}/stock-adjustments")]
        [ProducesResponseType(typeof(AdjustStockResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> AdjustStock(Guid serviceId, [FromBody] AdjustStockCommand command, CancellationToken cancellationToken)
        {
            if (!TryGetOperatorId(out var operatorId))
            {
                return Forbid();
            }

            command.ServiceId = serviceId;
            command.OperatorId = operatorId;
            return await SendAsync(command, cancellationToken);
        }

        /// <summary>Lấy lịch sử thay đổi tồn kho của dịch vụ.</summary>
        [HttpGet("{serviceId:guid}/inventory-history")]
        [ProducesResponseType(typeof(PagedResponse<GetInventoryHistoryResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetInventoryHistory(Guid serviceId, [FromQuery] GetInventoryHistoryQuery query, CancellationToken cancellationToken)
        {
            query.ServiceId = serviceId;
            return await SendAsync(query, cancellationToken);
        }

        private bool TryGetOperatorId(out Guid operatorId)
        {
            return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out operatorId);
        }

        private async Task<IActionResult> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _sender.Send(request, cancellationToken));
            }
            catch (ValidationException exception)
            {
                return BadRequest(CreateValidationError(exception));
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (ServiceConflictException exception)
            {
                return Conflict(new { message = exception.Message });
            }
        }

        private static object CreateValidationError(ValidationException exception)
        {
            return new
            {
                message = "Dữ liệu không hợp lệ.",
                errors = exception.Errors.GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())
            };
        }
    }
}

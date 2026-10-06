using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;
using SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/rentals")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
    public sealed class RentalsController : ControllerBase
    {
        private readonly ISender _sender;

        public RentalsController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lấy danh sách lượt thuê có phân trang và lọc.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<GetRentalsResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRentals([FromQuery] GetRentalsQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Đọc đầy đủ các lượt thuê của một sản phẩm trong đơn hàng.</summary>
        [HttpGet("orders/{orderId:guid}/products/{productId:guid}")]
        [ProducesResponseType(typeof(GetRentalDetailsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRentalDetails(Guid orderId, Guid productId, CancellationToken cancellationToken)
        {
            return await SendAsync(new GetRentalDetailsQuery { OrderId = orderId, ProductId = productId }, cancellationToken);
        }

        /// <summary>Tạo đơn thuê và trừ tồn kho theo giao dịch nguyên tử.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(CheckoutRentalResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRentalCommand command, CancellationToken cancellationToken)
        {
            if (!TryGetOperatorId(out var operatorId))
            {
                return Forbid();
            }

            command.OperatorId = operatorId;
            try
            {
                var result = await _sender.Send(command, cancellationToken);
                return Created($"/api/rentals/{result.Rentals[0].Id}", result);
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

        /// <summary>Trả một đơn vị thuê và cộng tồn kho theo giao dịch nguyên tử.</summary>
        [HttpPost("{rentalId:guid}/return")]
        [ProducesResponseType(typeof(ReturnRentalResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Return(Guid rentalId, CancellationToken cancellationToken)
        {
            if (!TryGetOperatorId(out var operatorId))
            {
                return Forbid();
            }

            return await SendAsync(new ReturnRentalCommand { RentalId = rentalId, OperatorId = operatorId }, cancellationToken);
        }

        /// <summary>Trả một số lượng lượt thuê đã chọn theo giao dịch nguyên tử.</summary>
        [HttpPost("orders/{orderId:guid}/products/{productId:guid}/returns")]
        [ProducesResponseType(typeof(ReturnRentalQuantityResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ReturnQuantity(
            Guid orderId,
            Guid productId,
            [FromBody] ReturnRentalQuantityCommand command,
            CancellationToken cancellationToken)
        {
            if (!TryGetOperatorId(out var operatorId))
            {
                return Forbid();
            }

            command.OrderId = orderId;
            command.ProductId = productId;
            command.OperatorId = operatorId;
            return await SendAsync(command, cancellationToken);
        }

        private bool TryGetOperatorId(out Guid operatorId)
        {
            return User.TryGetUserId(out operatorId);
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

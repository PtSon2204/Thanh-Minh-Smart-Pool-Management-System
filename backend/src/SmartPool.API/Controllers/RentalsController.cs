using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.QuoteRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;
using SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/rentals")]
    public sealed class RentalsController : ControllerBase
    {
        private readonly ISender _sender;

        public RentalsController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lấy danh sách lượt thuê có phân trang và lọc.</summary>
        [HttpGet]
        [RequirePermission("rentals", "read")]
        [ProducesResponseType(typeof(PagedResponse<GetRentalsResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRentals([FromQuery] GetRentalsQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Đọc đầy đủ các lượt thuê của một sản phẩm trong đơn hàng.</summary>
        [HttpGet("orders/{orderId:guid}/products/{productId:guid}")]
        [RequirePermission("rentals", "read")]
        [ProducesResponseType(typeof(GetRentalDetailsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRentalDetails(Guid orderId, Guid productId, CancellationToken cancellationToken)
        {
            return await SendAsync(new GetRentalDetailsQuery { OrderId = orderId, ProductId = productId }, cancellationToken);
        }

        [HttpPost("quote")]
        [RequirePermission("rentals", "create")]
        public Task<IActionResult> Quote([FromBody] QuoteRentalCommand command, CancellationToken cancellationToken) =>
            SendAsync(command, cancellationToken);

        /// <summary>Tạo đơn thuê và trừ tồn kho theo giao dịch nguyên tử.</summary>
        [HttpPost]
        [RequirePermission("rentals", "create")]
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
        [RequirePermission("rentals", "update")]
        [ProducesResponseType(typeof(ReturnRentalResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Return(Guid rentalId, [FromBody] ReturnRentalCommand command, CancellationToken cancellationToken)
        {
            if (!TryGetOperatorId(out var operatorId))
            {
                return Forbid();
            }

            command.RentalId = rentalId;
            command.OperatorId = operatorId;
            return await SendAsync(command, cancellationToken);
        }

        /// <summary>Trả một số lượng lượt thuê đã chọn theo giao dịch nguyên tử.</summary>
        [HttpPost("orders/{orderId:guid}/products/{productId:guid}/returns")]
        [RequirePermission("rentals", "update")]
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

        /// <summary>Tạo mã QR chuyển khoản và giữ tồn kho trong thời hạn thanh toán.</summary>
        [HttpPost("payments/qr")]
        [RequirePermission("rentals", "create")]
        [ProducesResponseType(typeof(RentalPaymentStatusResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreatePaymentQr([FromBody] CreateRentalQrCommand command, CancellationToken cancellationToken)
        {
            if (!TryGetOperatorId(out var operatorId))
            {
                return Forbid();
            }

            command.OperatorId = operatorId;
            return await SendCreatedAsync(
                result => $"/api/rentals/payments/{result.Id}",
                command,
                cancellationToken);
        }

        /// <summary>Lấy danh sách phiếu thanh toán thuê đang chờ hoặc cần đối soát.</summary>
        [HttpGet("payments")]
        [RequirePermission("rentals", "read")]
        [ProducesResponseType(typeof(PagedResponse<RentalPaymentStatusResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPayments([FromQuery] ListRentalPaymentsQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Lấy trạng thái mới nhất của một phiếu thanh toán thuê.</summary>
        [HttpGet("payments/{id:guid}")]
        [RequirePermission("rentals", "read")]
        [ProducesResponseType(typeof(RentalPaymentStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPayment(Guid id, CancellationToken cancellationToken)
        {
            return await SendAsync(new GetRentalPaymentQuery { Id = id }, cancellationToken);
        }

        /// <summary>Xác nhận phiếu đã thanh toán đủ và tạo lượt thuê.</summary>
        [HttpPost("payments/{id:guid}/issue")]
        [RequirePermission("rentals", "create")]
        [ProducesResponseType(typeof(CheckoutRentalResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> IssuePayment(Guid id, CancellationToken cancellationToken)
        {
            if (!TryGetOperatorId(out var operatorId))
            {
                return Forbid();
            }

            return await SendAsync(
                new IssueRentalPaymentCommand { Id = id, OperatorId = operatorId },
                cancellationToken);
        }

        /// <summary>Hủy phiếu thanh toán chưa hoàn tất và trả lại tồn kho đã giữ.</summary>
        [HttpPost("payments/{id:guid}/cancel")]
        [RequirePermission("rentals", "create")]
        [ProducesResponseType(typeof(RentalPaymentStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelPayment(Guid id, CancellationToken cancellationToken)
        {
            if (!TryGetOperatorId(out var operatorId))
            {
                return Forbid();
            }

            return await SendAsync(
                new CancelRentalPaymentCommand { Id = id, OperatorId = operatorId },
                cancellationToken);
        }

        private bool TryGetOperatorId(out Guid operatorId)
        {
            return User.TryGetUserId(out operatorId);
        }

        private Task<IActionResult> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            return SendAsync(request, result => Ok(result), cancellationToken);
        }

        private Task<IActionResult> SendCreatedAsync<TResponse>(
            Func<TResponse, string> location,
            IRequest<TResponse> request,
            CancellationToken cancellationToken)
        {
            return SendAsync(request, result => Created(location(result), result), cancellationToken);
        }

        private async Task<IActionResult> SendAsync<TResponse>(
            IRequest<TResponse> request,
            Func<TResponse, IActionResult> onSuccess,
            CancellationToken cancellationToken)
        {
            try
            {
                return onSuccess(await _sender.Send(request, cancellationToken));
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

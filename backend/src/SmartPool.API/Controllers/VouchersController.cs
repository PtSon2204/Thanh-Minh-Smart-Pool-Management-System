using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageVouchers;
using SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher;
using SmartPool.Application.Features.ManageVouchers.Commands.DeleteVoucher;
using SmartPool.Application.Features.ManageVouchers.Commands.ToggleLockVoucher;
using SmartPool.Application.Features.ManageVouchers.Commands.UpdateVoucher;
using SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/vouchers")]
    public sealed class VouchersController : ControllerBase
    {
        private readonly ISender _sender;

        public VouchersController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lấy danh sách voucher có phân trang và lọc.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<GetVouchersResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetVouchers([FromQuery] GetAllVouchersQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Tạo mới một voucher.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreateVoucherResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateVoucher([FromBody] CreateVoucherCommand command, CancellationToken cancellationToken)
        {
            return await SendAsync(command, cancellationToken, StatusCodes.Status201Created);
        }

        /// <summary>Cập nhật thông tin voucher.</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(UpdateVoucherResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateVoucher([FromRoute] Guid id, [FromBody] UpdateVoucherCommand command, CancellationToken cancellationToken)
        {
            command.Id = id;
            return await SendAsync(command, cancellationToken);
        }

        /// <summary>Khóa / mở khóa voucher (toggle IsActive).</summary>
        [HttpPut("{id:guid}/toggle-lock")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> ToggleLock([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            return await SendAsync(new ToggleLockVoucherCommand { Id = id }, cancellationToken);
        }

        /// <summary>Xóa mềm voucher (soft delete).</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteVoucher([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            return await SendAsync(new DeleteVoucherCommand { Id = id }, cancellationToken);
        }

        private async Task<IActionResult> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken, int successStatus = StatusCodes.Status200OK)
        {
            try
            {
                return StatusCode(successStatus, await _sender.Send(request, cancellationToken));
            }
            catch (ValidationException exception)
            {
                return BadRequest(CreateValidationError(exception));
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (VoucherConflictException exception)
            {
                return Conflict(new { message = exception.Message });
            }
        }

        private static object CreateValidationError(ValidationException exception)
        {
            return new
            {
                message = "Dữ liệu không hợp lệ.",
                errors = exception.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            };
        }
    }
}

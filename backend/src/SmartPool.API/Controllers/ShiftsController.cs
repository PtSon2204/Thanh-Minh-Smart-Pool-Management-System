using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.SaveShift;
using SmartPool.Application.Features.ManageStaffs.Queries.GetShifts;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/shifts")]
    public sealed class ShiftsController : ControllerBase
    {
        private readonly ISender _sender;

        public ShiftsController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lấy danh sách ca làm việc có phân trang và lọc.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<GetShiftsResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetShifts([FromQuery] GetShiftsQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Tạo ca làm việc.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(SaveShiftResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateShift([FromBody] SaveShiftCommand command, CancellationToken cancellationToken)
        {
            return await SendAsync(command, cancellationToken, StatusCodes.Status201Created);
        }

        /// <summary>Cập nhật ca làm việc.</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(SaveShiftResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateShift(Guid id, [FromBody] SaveShiftCommand command, CancellationToken cancellationToken)
        {
            command.Id = id;
            return await SendAsync(command, cancellationToken);
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
            catch (StaffConflictException exception)
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

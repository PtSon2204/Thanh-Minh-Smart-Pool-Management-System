using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.UpdateTicketType;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.ToggleLockTicketType;
using SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes;
using SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetTicketTypeById;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/ticket-types")]
    public class TicketTypeController : ControllerBase
    {
        private readonly ISender _sender;

        public TicketTypeController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lấy danh sách tất cả loại vé.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(SmartPool.Application.Common.Models.PagedResponse<GetAllTicketTypesResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] GetAllTicketTypesQuery query, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Lấy chi tiết 1 loại vé .</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(GetTicketTypeByIdResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sender.Send(new GetTicketTypeByIdQuery { Id = id }, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>Tạo mới loại vé.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreateTicketTypeResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create(
            [FromBody] CreateTicketTypeCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>Cập nhật loại vé.</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(UpdateTicketTypeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateTicketTypeCommand command,
            CancellationToken cancellationToken)
        {
            command.Id = id;
            try
            {
                var result = await _sender.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>Khóa / Mở khóa loại vé </summary>
        [HttpPut("{id:guid}/toggle-lock")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleLock(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sender.Send(new ToggleLockTicketTypeCommand { Id = id }, cancellationToken);
                return Ok(new { isActive = result, message = result ? "Đã mở khóa loại vé." : "Đã khóa loại vé." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>Xóa mềm loại vé (Soft Delete).</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sender.Send(new SmartPool.Application.Features.ManageTickets.TicketType.Commands.DeleteTicketType.DeleteTicketTypeCommand { Id = id }, cancellationToken);
                return Ok(new { success = result, message = "Đã xóa loại vé." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}


using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType;
using SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes;

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

        [HttpGet]
        [ProducesResponseType(typeof(List<GetAllTicketTypesResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var result = await _sender.Send(new GetAllTicketTypesQuery(), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(CreateTicketTypeResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateTicketTypeCommand command, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }
    }
}

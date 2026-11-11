using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.ToggleLockTicketType
{
    public class ToggleLockTicketTypeHandler : IRequestHandler<ToggleLockTicketTypeCommand, bool>
    {
        private readonly ITicketTypeOperations _operations;

        public ToggleLockTicketTypeHandler(ITicketTypeOperations operations)
        {
            _operations = operations;
        }

        public async Task<bool> Handle(ToggleLockTicketTypeCommand request, CancellationToken cancellationToken)
        {
            return await _operations.ToggleLockAsync(request.Id, cancellationToken);
        }
    }
}

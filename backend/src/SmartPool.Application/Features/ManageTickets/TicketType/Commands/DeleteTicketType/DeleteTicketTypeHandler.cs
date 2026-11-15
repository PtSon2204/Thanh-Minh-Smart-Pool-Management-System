using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.DeleteTicketType
{
    public class DeleteTicketTypeHandler : IRequestHandler<DeleteTicketTypeCommand, bool>
    {
        private readonly ITicketTypeOperations _operations;

        public DeleteTicketTypeHandler(ITicketTypeOperations operations)
        {
            _operations = operations;
        }

        public async Task<bool> Handle(DeleteTicketTypeCommand request, CancellationToken cancellationToken)
        {
            return await _operations.DeleteAsync(request.Id, cancellationToken);
        }
    }
}

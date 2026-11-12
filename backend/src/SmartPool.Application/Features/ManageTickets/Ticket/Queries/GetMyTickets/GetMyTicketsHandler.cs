using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyTickets
{
    public class GetMyTicketsHandler : IRequestHandler<GetMyTicketsQuery, List<MyTicketDto>>
    {
        private readonly ITicketOrderOperations _ticketOrderOperations;
        private readonly ITicketTypeOperations _ticketTypeOperations;
        private readonly IUserOperations _userOperations;

        public GetMyTicketsHandler(
            ITicketOrderOperations ticketOrderOperations,
            ITicketTypeOperations ticketTypeOperations,
            IUserOperations userOperations)
        {
            _ticketOrderOperations = ticketOrderOperations;
            _ticketTypeOperations = ticketTypeOperations;
            _userOperations = userOperations;
        }

        public async Task<List<MyTicketDto>> Handle(GetMyTicketsQuery request, CancellationToken cancellationToken)
        {
            var tickets = await _ticketOrderOperations.GetTicketsByUserIdAsync(request.UserId, cancellationToken);
            
            var user = await _userOperations.GetByIdAsync(request.UserId, cancellationToken);
            var ticketTypes = await _ticketTypeOperations.GetAllAsync(cancellationToken);
            var typeDict = ticketTypes.ToDictionary(t => t.Id);

            var result = new List<MyTicketDto>();
            foreach (var t in tickets.OrderByDescending(x => x.CreatedAt))
            {
                var type = typeDict.ContainsKey(t.TicketTypeId) ? typeDict[t.TicketTypeId] : null;
                result.Add(new MyTicketDto
                {
                    Id = t.Id,
                    QrCode = t.QrCode,
                    TicketName = type?.Name ?? "Unknown",
                    TicketCategory = type?.TicketCategory ?? "",
                    IssueDate = t.IssueDate,
                    ExpiryDate = t.ExpiryDate,
                    RemainingEntries = t.RemainingEntries,
                    TotalEntries = type?.TotalEntries,
                    Status = t.Status ?? "",
                    CustomerName = user?.Username ?? ""
                });
            }

            return result;
        }
    }
}

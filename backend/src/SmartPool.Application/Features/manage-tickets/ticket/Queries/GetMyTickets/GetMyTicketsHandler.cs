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
        private readonly IRepository<Domain.Entities.Ticket> _ticketRepo;
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IRepository<Domain.Entities.User> _userRepo;

        public GetMyTicketsHandler(
            IRepository<Domain.Entities.Ticket> ticketRepo,
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.User> userRepo)
        {
            _ticketRepo = ticketRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _userRepo = userRepo;
        }

        public async Task<List<MyTicketDto>> Handle(GetMyTicketsQuery request, CancellationToken cancellationToken)
        {
            var tickets = await _ticketRepo.FindAsync(t => t.UserId == request.UserId && t.IsDeleted != true, cancellationToken);
            
            var user = await _userRepo.GetByIdAsync(request.UserId, cancellationToken);
            var ticketTypes = await _ticketTypeRepo.GetAllAsync(cancellationToken);
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
                    Status = t.Status ?? "",
                    CustomerName = user?.Username ?? ""
                });
            }

            return result;
        }
    }
}

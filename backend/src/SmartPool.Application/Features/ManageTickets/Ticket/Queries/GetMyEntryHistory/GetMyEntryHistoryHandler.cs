using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyEntryHistory
{
    public class GetMyEntryHistoryHandler : IRequestHandler<GetMyEntryHistoryQuery, List<MyEntryHistoryDto>>
    {
        private readonly IRepository<Domain.Entities.Ticket> _ticketRepo;
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IRepository<Domain.Entities.EntryLog> _entryLogRepo;

        public GetMyEntryHistoryHandler(
            IRepository<Domain.Entities.Ticket> ticketRepo,
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.EntryLog> entryLogRepo)
        {
            _ticketRepo = ticketRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _entryLogRepo = entryLogRepo;
        }

        public async Task<List<MyEntryHistoryDto>> Handle(GetMyEntryHistoryQuery request, CancellationToken cancellationToken)
        {
            var myTicketsEnum = await _ticketRepo.FindAsync(t => t.UserId == request.UserId && t.IsDeleted != true, cancellationToken);
            var myTickets = myTicketsEnum?.ToList() ?? new List<Domain.Entities.Ticket>();
            if (myTickets.Count == 0) return new List<MyEntryHistoryDto>();

            var ticketDict = myTickets.ToDictionary(t => t.Id);
            var ticketIds = ticketDict.Keys.ToList();

            var ticketTypesEnum = await _ticketTypeRepo.GetAllAsync(cancellationToken);
            var ticketTypes = ticketTypesEnum?.ToList() ?? new List<Domain.Entities.TicketType>();
            var typeDict = ticketTypes.ToDictionary(t => t.Id);

            var entryLogsEnum = await _entryLogRepo.FindAsync(
                e => e.TicketId.HasValue && ticketIds.Contains(e.TicketId.Value),
                cancellationToken);
            var entryLogs = entryLogsEnum?.ToList() ?? new List<Domain.Entities.EntryLog>();

            var result = new List<MyEntryHistoryDto>();
            foreach (var log in entryLogs.OrderByDescending(e => e.ScanTime))
            {
                if (!ticketDict.TryGetValue(log.TicketId!.Value, out var ticket)) continue;
                typeDict.TryGetValue(ticket.TicketTypeId, out var type);

                result.Add(new MyEntryHistoryDto
                {
                    Id = log.Id,
                    TicketId = ticket.Id,
                    TicketName = type?.Name ?? "Vé vào bể",
                    TicketCategory = type?.TicketCategory ?? "",
                    QrCode = ticket.QrCode,
                    ScanTime = log.ScanTime,
                    Status = log.Status ?? "Allowed",
                    InputMode = log.InputMode,
                    GateId = log.GateId,
                    Message = log.Message
                });
            }

            return result;
        }
    }
}

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
        private readonly ITicketOrderOperations _ticketOrderOperations;
        private readonly ITicketTypeOperations _ticketTypeOperations;

        public GetMyEntryHistoryHandler(
            ITicketOrderOperations ticketOrderOperations,
            ITicketTypeOperations ticketTypeOperations)
        {
            _ticketOrderOperations = ticketOrderOperations;
            _ticketTypeOperations = ticketTypeOperations;
        }

        public async Task<List<MyEntryHistoryDto>> Handle(GetMyEntryHistoryQuery request, CancellationToken cancellationToken)
        {
            var myTickets = await _ticketOrderOperations.GetTicketsByUserIdAsync(request.UserId, cancellationToken);
            if (myTickets.Count == 0) return new List<MyEntryHistoryDto>();

            var ticketDict = myTickets.ToDictionary(t => t.Id);
            var ticketIds = ticketDict.Keys.ToList();

            var ticketTypes = await _ticketTypeOperations.GetAllAsync(cancellationToken);
            var typeDict = ticketTypes.ToDictionary(t => t.Id);

            var entryLogs = await _ticketOrderOperations.GetEntryLogsByTicketIdsAsync(ticketIds, cancellationToken);

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

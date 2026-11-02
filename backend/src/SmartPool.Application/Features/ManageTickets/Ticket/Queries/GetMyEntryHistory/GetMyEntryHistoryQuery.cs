using MediatR;
using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyEntryHistory
{
    public class GetMyEntryHistoryQuery : IRequest<List<MyEntryHistoryDto>>
    {
        public Guid UserId { get; set; }
    }
}

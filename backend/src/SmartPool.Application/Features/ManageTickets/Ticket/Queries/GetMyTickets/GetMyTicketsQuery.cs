using MediatR;
using System.Collections.Generic;
using System;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyTickets
{
    public class GetMyTicketsQuery : IRequest<List<MyTicketDto>>
    {
        [JsonIgnore]
        public Guid UserId { get; set; }
    }
}

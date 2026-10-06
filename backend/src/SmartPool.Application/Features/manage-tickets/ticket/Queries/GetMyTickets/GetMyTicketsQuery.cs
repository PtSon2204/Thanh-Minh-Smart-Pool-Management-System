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

    public class MyTicketDto
    {
        public Guid Id { get; set; }
        public string QrCode { get; set; } = string.Empty;
        public string TicketName { get; set; } = string.Empty;
        public string TicketCategory { get; set; } = string.Empty;
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
    }
}

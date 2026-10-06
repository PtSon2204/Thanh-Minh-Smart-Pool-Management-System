using MediatR;
using System.Collections.Generic;
using System;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.CheckoutOnline
{
    public class CheckoutOnlineCommand : IRequest<CheckoutOnlineResponse>
    {
        [JsonIgnore]
        public Guid UserId { get; set; }

        public List<CheckoutItemDto> Items { get; set; } = new List<CheckoutItemDto>();
        public string? VoucherCode { get; set; }
    }

    public class CheckoutItemDto
    {
        public Guid TicketTypeId { get; set; }
        public int Quantity { get; set; }
    }
}

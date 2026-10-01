using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket
{
    public class LookupTicketQuery : IRequest<PoolAccessValidationResult<LookupTicketResponse>>
    {
        public string? Code { get; set; }
    }
}

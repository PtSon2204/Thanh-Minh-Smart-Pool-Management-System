using AutoMapper;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes
{
    public class GetAllTicketTypesHandler : IRequestHandler<GetAllTicketTypesQuery, List<GetAllTicketTypesResponse>>
    {
        private readonly IRepository<SmartPool.Domain.Entities.TicketType> _repo;
        private readonly IMapper _mapper;

        public GetAllTicketTypesHandler(
            IRepository<SmartPool.Domain.Entities.TicketType> repo,
            IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        public async Task<List<GetAllTicketTypesResponse>> Handle(
            GetAllTicketTypesQuery request,
            CancellationToken cancellationToken)
        {
            var entities = await _repo.FindAsync(t => t.IsDeleted != true, cancellationToken);

            return _mapper.Map<List<GetAllTicketTypesResponse>>(entities);
        }
    }
}


using AutoMapper;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetTicketTypeById
{
    public class GetTicketTypeByIdHandler
        : IRequestHandler<GetTicketTypeByIdQuery, GetTicketTypeByIdResponse>
    {
        private readonly IRepository<TicketTypeEntity> _repo;
        private readonly IMapper _mapper;

        public GetTicketTypeByIdHandler(IRepository<TicketTypeEntity> repo, IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        public async Task<GetTicketTypeByIdResponse> Handle(
            GetTicketTypeByIdQuery request,
            CancellationToken cancellationToken)
        {
            var entity = await _repo.FirstOrDefaultAsync(
                t => t.Id == request.Id && t.IsDeleted != true,
                cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy loại vé với Id: {request.Id}");

            return _mapper.Map<GetTicketTypeByIdResponse>(entity);
        }
    }
}

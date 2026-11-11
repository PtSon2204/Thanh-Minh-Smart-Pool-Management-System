using AutoMapper;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetTicketTypeById
{
    public class GetTicketTypeByIdHandler
        : IRequestHandler<GetTicketTypeByIdQuery, GetTicketTypeByIdResponse>
    {
        private readonly ITicketTypeOperations _operations;
        private readonly IMapper _mapper;

        public GetTicketTypeByIdHandler(ITicketTypeOperations operations, IMapper mapper)
        {
            _operations = operations;
            _mapper = mapper;
        }

        public async Task<GetTicketTypeByIdResponse> Handle(
            GetTicketTypeByIdQuery request,
            CancellationToken cancellationToken)
        {
            var entity = await _operations.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy loại vé với Id: {request.Id}");

            return _mapper.Map<GetTicketTypeByIdResponse>(entity);
        }
    }
}

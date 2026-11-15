using AutoMapper;
using MediatR;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType
{
    public class CreateTicketTypeHandler : IRequestHandler<CreateTicketTypeCommand, CreateTicketTypeResponse>
    {
        private readonly ITicketTypeOperations _operations;
        private readonly IMapper _mapper;

        public CreateTicketTypeHandler(ITicketTypeOperations operations, IMapper mapper)
        {
            _operations = operations;
            _mapper = mapper;
        }

        public async Task<CreateTicketTypeResponse> Handle(CreateTicketTypeCommand request, CancellationToken cancellationToken)
        {
            int? totalEntries = request.TicketCategory == TicketCategoryEnum.VE_LUOT
                ? 1
                : null;

            var entity = new TicketTypeEntity
            {
                Name           = request.Name,
                TicketCategory = request.TicketCategory.ToString(), // Enum → string
                Price          = request.Price,
                TotalEntries   = totalEntries,
                DurationDays   = TicketValidityPolicy.GetDurationDays(request.TicketCategory, totalEntries),
            };
                
            await _operations.AddAsync(entity, cancellationToken);

            return _mapper.Map<CreateTicketTypeResponse>(entity);
        }
    }
}

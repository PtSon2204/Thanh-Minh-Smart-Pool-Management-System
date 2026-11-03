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
        private readonly IRepository<TicketTypeEntity> _repo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CreateTicketTypeHandler(IRepository<TicketTypeEntity> repo, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<CreateTicketTypeResponse> Handle(CreateTicketTypeCommand request, CancellationToken cancellationToken)
        {
            int? totalEntries = request.TicketCategory == TicketCategoryEnum.VE_LUOT
                ? (request.TotalEntries ?? 1)
                : null;

            var entity = new TicketTypeEntity
            {
                Name           = request.Name,
                TicketCategory = request.TicketCategory.ToString(), // Enum → string
                Price          = request.Price,
                TotalEntries   = totalEntries,
                DurationDays   = TicketValidityPolicy.GetDurationDays(request.TicketCategory, totalEntries),
            };
                
            await _repo.AddAsync(entity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<CreateTicketTypeResponse>(entity);
        }
    }
}

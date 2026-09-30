using AutoMapper;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.UpdateTicketType
{
    public class UpdateTicketTypeHandler
        : IRequestHandler<UpdateTicketTypeCommand, UpdateTicketTypeResponse>
    {
        private readonly IRepository<TicketTypeEntity> _repo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public UpdateTicketTypeHandler(
            IRepository<TicketTypeEntity> repo,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<UpdateTicketTypeResponse> Handle(
            UpdateTicketTypeCommand request,
            CancellationToken cancellationToken)
        {
            var entity = await _repo.FirstOrDefaultAsync(
                t => t.Id == request.Id && t.IsDeleted != true,
                cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy loại vé với Id: {request.Id}");

            // Cập nhật các trường
            entity.Name           = request.Name;
            entity.TicketCategory = request.TicketCategory.ToString();
            entity.Price          = request.Price;
            entity.DurationDays   = request.DurationDays;
            entity.UpdatedAt      = DateTime.UtcNow;

            _repo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<UpdateTicketTypeResponse>(entity);
        }
    }
}

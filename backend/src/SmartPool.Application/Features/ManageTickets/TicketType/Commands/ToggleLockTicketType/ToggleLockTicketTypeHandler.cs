using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.ToggleLockTicketType
{
    public class ToggleLockTicketTypeHandler : IRequestHandler<ToggleLockTicketTypeCommand, bool>
    {
        private readonly IRepository<TicketTypeEntity> _repo;
        private readonly IUnitOfWork _unitOfWork;

        public ToggleLockTicketTypeHandler(IRepository<TicketTypeEntity> repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(ToggleLockTicketTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _repo.FirstOrDefaultAsync(
                t => t.Id == request.Id && t.IsDeleted != true,
                cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy loại vé với Id: {request.Id}");

            // Đảo ngược trạng thái
            entity.IsActive = !(entity.IsActive ?? true);
            entity.UpdatedAt = DateTime.UtcNow;

            _repo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return entity.IsActive.Value;
        }
    }
}

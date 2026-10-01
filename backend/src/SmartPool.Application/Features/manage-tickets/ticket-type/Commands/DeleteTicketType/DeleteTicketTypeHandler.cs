using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.DeleteTicketType
{
    public class DeleteTicketTypeHandler : IRequestHandler<DeleteTicketTypeCommand, bool>
    {
        private readonly IRepository<TicketTypeEntity> _repo;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteTicketTypeHandler(IRepository<TicketTypeEntity> repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(DeleteTicketTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _repo.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy loại vé với Id: {request.Id}");

            // Soft delete
            entity.IsDeleted = true;
            entity.IsActive = false; // Disable it as well
            entity.UpdatedAt = DateTime.UtcNow;

            _repo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}

using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using VoucherEntity = SmartPool.Domain.Entities.Voucher;

namespace SmartPool.Application.Features.ManageVouchers.Commands.DeleteVoucher
{
    public sealed class DeleteVoucherHandler : IRequestHandler<DeleteVoucherCommand, bool>
    {
        private readonly IRepository<VoucherEntity> _repo;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteVoucherHandler(IRepository<VoucherEntity> repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(DeleteVoucherCommand request, CancellationToken cancellationToken)
        {
            var entity = await _repo.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy voucher với Id: {request.Id}");

            // Soft delete
            entity.IsDeleted = true;
            entity.IsActive = false;

            _repo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}

using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using VoucherEntity = SmartPool.Domain.Entities.Voucher;

namespace SmartPool.Application.Features.ManageVouchers.Commands.ToggleLockVoucher
{
    public sealed class ToggleLockVoucherHandler : IRequestHandler<ToggleLockVoucherCommand, bool>
    {
        private readonly IRepository<VoucherEntity> _repo;
        private readonly IUnitOfWork _unitOfWork;

        public ToggleLockVoucherHandler(IRepository<VoucherEntity> repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(ToggleLockVoucherCommand request, CancellationToken cancellationToken)
        {
            var entity = await _repo.FirstOrDefaultAsync(
                v => v.Id == request.Id && v.IsDeleted != true,
                cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy voucher với Id: {request.Id}");

            // Đảo ngược IsActive (khóa ↔ mở khóa)
            entity.IsActive = !(entity.IsActive ?? true);

            _repo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return entity.IsActive.Value;
        }
    }
}

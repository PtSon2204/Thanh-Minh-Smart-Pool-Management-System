using AutoMapper;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using VoucherEntity = SmartPool.Domain.Entities.Voucher;

namespace SmartPool.Application.Features.ManageVouchers.Commands.UpdateVoucher
{
    public sealed class UpdateVoucherHandler : IRequestHandler<UpdateVoucherCommand, UpdateVoucherResponse>
    {
        private readonly IRepository<VoucherEntity> _repo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public UpdateVoucherHandler(IRepository<VoucherEntity> repo, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<UpdateVoucherResponse> Handle(UpdateVoucherCommand request, CancellationToken cancellationToken)
        {
            var entity = await _repo.FirstOrDefaultAsync(
                v => v.Id == request.Id && v.IsDeleted != true,
                cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy voucher với Id: {request.Id}");

            // Kiểm tra trùng Code nếu Code thay đổi
            var codeConflict = await _repo.FirstOrDefaultAsync(
                v => v.Code == request.Code && v.Id != request.Id && v.IsDeleted != true,
                cancellationToken);
            if (codeConflict is not null)
                throw new VoucherConflictException($"Mã voucher '{request.Code}' đã tồn tại.");

            entity.Code = request.Code.Trim().ToUpper();
            entity.DiscountType = request.DiscountType;
            entity.DiscountValue = request.DiscountValue;
            entity.MinOrderValue = request.MinOrderValue;
            entity.StartDate = request.StartDate;
            entity.EndDate = request.EndDate;
            entity.IsActive = request.IsActive;

            _repo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<UpdateVoucherResponse>(entity);
        }
    }
}

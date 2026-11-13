using AutoMapper;
using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using VoucherEntity = SmartPool.Domain.Entities.Voucher;

namespace SmartPool.Application.Features.ManageVouchers.Commands.UpdateVoucher
{
    public sealed class UpdateVoucherHandler : IRequestHandler<UpdateVoucherCommand, UpdateVoucherResponse>
    {
        private readonly IVoucherOperations _operations;
        private readonly IMapper _mapper;
        private readonly IValidator<UpdateVoucherCommand>? _validator;

        public UpdateVoucherHandler(
            IVoucherOperations operations,
            IMapper mapper, 
            IValidator<UpdateVoucherCommand>? validator = null)
        {
            _operations = operations;
            _mapper = mapper;
            _validator = validator;
        }

        public async Task<UpdateVoucherResponse> Handle(UpdateVoucherCommand request, CancellationToken cancellationToken)
        {
            if (_validator != null)
            {
                await _validator.ValidateAndThrowAsync(request, cancellationToken);
            }

            var entity = await _operations.GetVoucherByIdAsync(request.Id, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy voucher với Id: {request.Id}");

            // Kiểm tra trùng Code nếu Code thay đổi
            var codeConflict = await _operations.ExistsByCodeAsync(request.Code, request.Id, cancellationToken);
            if (codeConflict)
                throw new VoucherConflictException($"Mã voucher '{request.Code}' đã tồn tại.");

            entity.Code = request.Code.Trim().ToUpper();
            entity.DiscountType = request.DiscountType;
            entity.DiscountValue = request.DiscountValue;
            entity.MinOrderValue = request.MinOrderValue;
            entity.StartDate = request.StartDate.HasValue 
                ? (request.StartDate.Value.Kind == DateTimeKind.Unspecified 
                    ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc) 
                    : request.StartDate.Value.ToUniversalTime()) 
                : null;
            entity.EndDate = request.EndDate.HasValue 
                ? (request.EndDate.Value.Kind == DateTimeKind.Unspecified 
                    ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) 
                    : request.EndDate.Value.ToUniversalTime()) 
                : null;
            entity.IsActive = request.IsActive;

            await _operations.UpdateVoucherAsync(entity, cancellationToken);

            return _mapper.Map<UpdateVoucherResponse>(entity);
        }
    }
}

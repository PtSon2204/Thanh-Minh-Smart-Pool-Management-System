using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher;
using SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories
{
    public interface IVoucherOperations
    {
        Task<PagedResponse<GetVouchersResponse>> GetAllVouchersAsync(GetAllVouchersQuery request, CancellationToken cancellationToken);
        Task<CreateVoucherResponse> CreateVoucherAsync(CreateVoucherCommand request, CancellationToken cancellationToken);
        Task<Voucher?> GetVoucherByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Voucher?> GetUsableVoucherByCodeAsync(string code, DateTime nowUtc, CancellationToken cancellationToken = default);
        Task<bool> ExistsByCodeAsync(string code, Guid excludeId, CancellationToken cancellationToken = default);
        Task UpdateVoucherAsync(Voucher voucher, CancellationToken cancellationToken = default);
        Task<bool> DeleteVoucherAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ToggleLockVoucherAsync(Guid id, CancellationToken cancellationToken = default);
    }
}

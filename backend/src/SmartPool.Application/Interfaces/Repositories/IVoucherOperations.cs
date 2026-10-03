using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher;
using SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers;

namespace SmartPool.Application.Interfaces.Repositories
{
    public interface IVoucherOperations
    {
        Task<PagedResponse<GetVouchersResponse>> GetAllVouchersAsync(GetAllVouchersQuery request, CancellationToken cancellationToken);
        Task<CreateVoucherResponse> CreateVoucherAsync(CreateVoucherCommand request, CancellationToken cancellationToken);
    }
}

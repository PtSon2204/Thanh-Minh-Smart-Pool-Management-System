using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.GoodsReceipts;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IGoodsReceiptOperations
{
    Task<GoodsReceiptResponse> CreateAsync(CreateGoodsReceiptCommand command, CancellationToken cancellationToken);
    Task<PagedResponse<GoodsReceiptResponse>> GetAllAsync(GetGoodsReceiptsQuery query, CancellationToken cancellationToken);
    Task<GoodsReceiptResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<GoodsReceiptResponse> PostAsync(Guid id, Guid operatorId, CancellationToken cancellationToken);
    Task<GoodsReceiptResponse> CancelAsync(Guid id, CancellationToken cancellationToken);
}

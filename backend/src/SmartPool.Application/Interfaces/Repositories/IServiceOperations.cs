using SmartPool.Application.Features.ManageServices.Contracts;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IServiceOperations
{
    Task<ServiceOperationResult<PageDto<ServiceDto>>> GetServicesAsync(ServiceListQuery query, CancellationToken cancellationToken);
    Task<ServiceOperationResult<ServiceDto>> CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken);
    Task<ServiceOperationResult<ServiceDto>> UpdateServiceAsync(Guid serviceId, UpdateServiceRequest request, CancellationToken cancellationToken);
    Task<ServiceOperationResult<StockAdjustmentDto>> AdjustStockAsync(Guid serviceId, AdjustStockRequest request, Guid operatorId, CancellationToken cancellationToken);
    Task<ServiceOperationResult<PageDto<InventoryLogDto>>> GetInventoryHistoryAsync(Guid serviceId, InventoryHistoryQuery query, CancellationToken cancellationToken);
    Task<ServiceOperationResult<PageDto<RentalDto>>> GetRentalsAsync(RentalListQuery query, CancellationToken cancellationToken);
    Task<ServiceOperationResult<RentalCheckoutDto>> CheckoutRentalAsync(RentalCheckoutRequest request, Guid operatorId, CancellationToken cancellationToken);
    Task<ServiceOperationResult<RentalReturnDto>> ReturnRentalAsync(Guid rentalId, Guid operatorId, CancellationToken cancellationToken);
}

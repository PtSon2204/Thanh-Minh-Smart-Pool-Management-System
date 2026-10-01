using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices.Commands.AdjustStock;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Commands.UpdateService;
using SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;

namespace SmartPool.Application.Interfaces.Repositories
{

public interface IServiceOperations
{
    Task<UpdateServiceResponse> UpdateServiceAsync(UpdateServiceCommand command, CancellationToken cancellationToken);
    Task<AdjustStockResponse> AdjustStockAsync(AdjustStockCommand command, CancellationToken cancellationToken);
    Task<PagedResponse<GetInventoryHistoryResponse>> GetInventoryHistoryAsync(GetInventoryHistoryQuery query, CancellationToken cancellationToken);
    Task<PagedResponse<GetRentalsResponse>> GetRentalsAsync(GetRentalsQuery query, CancellationToken cancellationToken);
    Task<CheckoutRentalResponse> CheckoutRentalAsync(CheckoutRentalCommand command, CancellationToken cancellationToken);
    Task<ReturnRentalResponse> ReturnRentalAsync(ReturnRentalCommand command, CancellationToken cancellationToken);
}
}

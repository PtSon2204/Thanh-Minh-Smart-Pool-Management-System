using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices.Commands.AdjustStock;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Application.Features.ManageServices.Commands.SetServiceStatus;
using SmartPool.Application.Features.ManageServices.Commands.UpdateService;
using SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;
using SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

namespace SmartPool.Application.Interfaces.Repositories
{

public interface IServiceOperations
{
    Task<UpdateServiceResponse> UpdateServiceAsync(UpdateServiceCommand command, CancellationToken cancellationToken);
    Task<SetServiceStatusResponse> SetServiceStatusAsync(SetServiceStatusCommand command, CancellationToken cancellationToken);
    Task<AdjustStockResponse> AdjustStockAsync(AdjustStockCommand command, CancellationToken cancellationToken);
    Task<PagedResponse<GetInventoryHistoryResponse>> GetInventoryHistoryAsync(GetInventoryHistoryQuery query, CancellationToken cancellationToken);
    Task<PagedResponse<GetRentalsResponse>> GetRentalsAsync(GetRentalsQuery query, CancellationToken cancellationToken);
    Task<GetRentalDetailsResponse> GetRentalDetailsAsync(GetRentalDetailsQuery query, CancellationToken cancellationToken);
    Task<CheckoutRentalResponse> CheckoutRentalAsync(CheckoutRentalCommand command, CancellationToken cancellationToken);
    Task<ReturnRentalResponse> ReturnRentalAsync(ReturnRentalCommand command, CancellationToken cancellationToken);
    Task<ReturnRentalQuantityResponse> ReturnRentalQuantityAsync(ReturnRentalQuantityCommand command, CancellationToken cancellationToken);
}
}

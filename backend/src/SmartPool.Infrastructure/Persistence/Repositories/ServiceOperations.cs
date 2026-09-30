using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.TempModels;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed partial class ServiceOperations(SmartPoolDbContext context) : IServiceOperations
{
    private readonly SmartPoolDbContext _context = context;

    private static ServiceDto ToDto(Product product) => new(product.Id, product.Name, product.Type, product.Price, product.StockQuantity, product.IsActive ?? false, product.IsDeleted ?? false, product.CreatedAt, product.UpdatedAt);
    private static InventoryLogDto ToDto(InventoryLog log) => new(log.Id, log.ProductId, log.ChangeType, log.Quantity, log.Note, log.CreatedBy, log.CreatedAt);
    private static RentalDto ToDto(Rental rental, string productName, string? orderStatus) => new(rental.Id, rental.OrderId, rental.ProductId, productName, rental.RentTime, rental.ReturnTime, rental.DepositAmount, rental.Status ?? string.Empty, orderStatus);
    private static ServiceOperationResult<T> NotFound<T>(string field) => ServiceOperationResult<T>.Failure(ServiceOperationError.NotFound, field, "The requested record was not found.");
    private static ServiceOperationResult<T> Conflict<T>(string field, string message) => ServiceOperationResult<T>.Failure(ServiceOperationError.Conflict, field, message);
}

using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Infrastructure.Persistence.TempModels;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed partial class ServiceOperations
{
    public async Task<ServiceOperationResult<PageDto<ServiceDto>>> GetServicesAsync(ServiceListQuery query, CancellationToken cancellationToken)
    {
        var products = _context.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search)) products = products.Where(product => EF.Functions.ILike(product.Name, $"%{query.Search.Trim()}%"));
        if (!string.IsNullOrWhiteSpace(query.Type)) products = products.Where(product => product.Type == query.Type);
        if (query.Active.HasValue) products = products.Where(product => product.IsActive == query.Active.Value);
        var total = await products.CountAsync(cancellationToken);
        var items = await products.OrderBy(product => product.Name).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(product => new ServiceDto(product.Id, product.Name, product.Type, product.Price, product.StockQuantity, product.IsActive ?? false, product.IsDeleted ?? false, product.CreatedAt, product.UpdatedAt)).ToListAsync(cancellationToken);
        return ServiceOperationResult<PageDto<ServiceDto>>.Success(new(items, total, query.Page, query.PageSize));
    }

    public async Task<ServiceOperationResult<ServiceDto>> CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var product = new Product { Id = Guid.NewGuid(), Name = request.Name.Trim(), Type = request.Type, Price = request.Price, StockQuantity = request.StockQuantity, IsActive = request.IsActive, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);
        return ServiceOperationResult<ServiceDto>.Success(ToDto(product));
    }

    public async Task<ServiceOperationResult<ServiceDto>> UpdateServiceAsync(Guid serviceId, UpdateServiceRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var product = await _context.Products
            .FromSqlInterpolated($"SELECT * FROM smart_pool.products WHERE id = {serviceId} AND is_deleted = false FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) return NotFound<ServiceDto>(nameof(serviceId));

        var hasActiveRentals = await _context.Rentals
            .AnyAsync(rental => rental.ProductId == serviceId && rental.Status == "Renting", cancellationToken);
        if (hasActiveRentals && (request.Type != "Rental" || product.StockQuantity is null))
        {
            return Conflict<ServiceDto>(nameof(request.Type), "A product with active rentals must remain a rental product with returnable stock.");
        }

        product.Name = request.Name.Trim();
        product.Type = request.Type;
        product.Price = request.Price;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceOperationResult<ServiceDto>.Success(ToDto(product));
    }
}

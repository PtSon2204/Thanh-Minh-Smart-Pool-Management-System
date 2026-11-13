using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CreateService;
using SmartPool.Application.Features.ManageServices.Commands.UpdateService;
using SmartPool.Application.Features.ManageServices.Commands.SetServiceStatus;
using SmartPool.Application.Features.ManageServices.Queries.GetServices;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services
{

public sealed partial class ServiceOperations
{
    public async Task<CreateServiceResponse> CreateServiceAsync(CreateServiceCommand command, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = command.Name.Trim(),
            Price = command.Price,
            StockQuantity = command.StockQuantity,
            IsActive = command.IsActive,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return _mapper.Map<CreateServiceResponse>(product);
    }

    public async Task<PagedResponse<GetServicesResponse>> GetServicesAsync(GetServicesQuery query, CancellationToken cancellationToken)
    {
        var productsQuery = _context.Products
            .AsNoTracking()
            .Where(product => product.IsDeleted != true);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim().ToLower();
            productsQuery = productsQuery.Where(product => product.Name != null && EF.Functions.ILike(product.Name, $"%{searchTerm}%"));
        }

        if (query.IsActive.HasValue)
        {
            productsQuery = query.IsActive.Value
                ? productsQuery.Where(product => product.IsActive == true || product.IsActive == null)
                : productsQuery.Where(product => product.IsActive == false);
        }

        var totalCount = await productsQuery.CountAsync(cancellationToken);
        var items = await productsQuery
            .OrderByDescending(product => product.CreatedAt)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<GetServicesResponse>
        {
            Items = _mapper.Map<List<GetServicesResponse>>(items),
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<UpdateServiceResponse> UpdateServiceAsync(UpdateServiceCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var product = await _context.Products
            .FromSqlInterpolated($"SELECT * FROM smart_pool.products WHERE id = {command.Id} AND is_deleted = false FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) throw new KeyNotFoundException("Không tìm thấy dịch vụ.");

        var hasActiveRentals = await _context.Rentals
            .AnyAsync(rental => rental.ProductId == command.Id && rental.Status == "Renting", cancellationToken);
        if (hasActiveRentals && product.StockQuantity is null)
        {
            throw new ServiceConflictException("Đồ đang cho thuê phải có số lượng để nhận trả.");
        }

        product.Name = command.Name.Trim();
        product.Price = command.Price;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return _mapper.Map<UpdateServiceResponse>(product);
    }

    public async Task<SetServiceStatusResponse> SetServiceStatusAsync(SetServiceStatusCommand command, CancellationToken cancellationToken)
    {
        var updatedAt = DateTime.UtcNow;
        var changed = await _context.Products
            .Where(product => product.Id == command.Id && product.IsDeleted != true)
            .ExecuteUpdateAsync(update => update
                .SetProperty(product => product.IsActive, (bool?)command.IsActive)
                .SetProperty(product => product.UpdatedAt, (DateTime?)updatedAt), cancellationToken);

        if (changed == 0) throw new KeyNotFoundException("Không tìm thấy dịch vụ.");

        return new SetServiceStatusResponse { Id = command.Id, IsActive = command.IsActive, UpdatedAt = updatedAt };
    }
}
}

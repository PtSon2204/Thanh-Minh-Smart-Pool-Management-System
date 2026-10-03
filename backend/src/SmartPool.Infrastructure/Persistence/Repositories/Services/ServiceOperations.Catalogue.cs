using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.UpdateService;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories
{

public sealed partial class ServiceOperations
{
    public async Task<UpdateServiceResponse> UpdateServiceAsync(UpdateServiceCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var product = await _context.Products
            .FromSqlInterpolated($"SELECT * FROM smart_pool.products WHERE id = {command.Id} AND is_deleted = false FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) throw new KeyNotFoundException("Không tìm thấy dịch vụ.");

        var hasActiveRentals = await _context.Rentals
            .AnyAsync(rental => rental.ProductId == command.Id && rental.Status == "Renting", cancellationToken);
        if (hasActiveRentals && (command.Type != "Rental" || product.StockQuantity is null))
        {
            throw new ServiceConflictException("Dịch vụ có lượt thuê đang hoạt động phải giữ loại Rental và tồn kho có thể hoàn trả.");
        }

        product.Name = command.Name.Trim();
        product.Type = command.Type;
        product.Price = command.Price;
        product.IsActive = command.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return _mapper.Map<UpdateServiceResponse>(product);
    }
}
}

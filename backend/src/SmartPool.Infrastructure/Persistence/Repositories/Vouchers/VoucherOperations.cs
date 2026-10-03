using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageVouchers;
using SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher;
using SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories
{
    public sealed partial class VoucherOperations : IVoucherOperations
    {
        private readonly SmartPoolDbContext context;
        private readonly IMapper mapper;

        public VoucherOperations(SmartPoolDbContext context, IMapper mapper)
        {
            this.context = context;
            this.mapper = mapper;
        }

        private static (int Page, int PageSize) Normalize(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

        public async Task<PagedResponse<GetVouchersResponse>> GetAllVouchersAsync(GetAllVouchersQuery request, CancellationToken cancellationToken)
        {
            var query = context.Vouchers.AsNoTracking().Where(v => v.IsDeleted != true).AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                query = query.Where(v => v.Code.Contains(request.SearchTerm));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(v => v.IsActive == request.IsActive.Value);
            }

            var total = await query.CountAsync(cancellationToken);
            var page = Normalize(request.PageIndex, request.PageSize);
            
            var rows = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page.Page - 1) * page.PageSize)
                .Take(page.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResponse<GetVouchersResponse>
            {
                Items = mapper.Map<List<GetVouchersResponse>>(rows),
                TotalCount = total,
                PageIndex = page.Page,
                PageSize = page.PageSize
            };
        }

        public async Task<CreateVoucherResponse> CreateVoucherAsync(CreateVoucherCommand request, CancellationToken cancellationToken)
        {
            var exists = await context.Vouchers.AnyAsync(v => v.Code == request.Code && v.IsDeleted != true, cancellationToken);
            if (exists)
            {
                throw new VoucherConflictException($"Voucher with code '{request.Code}' already exists.");
            }

            var voucher = new Voucher
            {
                Id = Guid.NewGuid(),
                Code = request.Code,
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                MinOrderValue = request.MinOrderValue,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = request.IsActive ?? true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await context.Vouchers.AddAsync(voucher, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return new CreateVoucherResponse
            {
                Id = voucher.Id,
                Code = voucher.Code,
                Message = "Tạo voucher thành công."
            };
        }
    }
}

using AutoMapper;
using SmartPool.Application.Common.Helpers;
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
        private readonly TimeProvider timeProvider;

        public VoucherOperations(SmartPoolDbContext context, IMapper mapper, TimeProvider timeProvider)
        {
            this.context = context;
            this.mapper = mapper;
            this.timeProvider = timeProvider;
        }

        private static (int Page, int PageSize) Normalize(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

        public async Task<PagedResponse<GetVouchersResponse>> GetAllVouchersAsync(GetAllVouchersQuery request, CancellationToken cancellationToken)
        {
            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            var query = context.Vouchers.AsNoTracking().Where(v => v.IsDeleted != true).AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                query = query.Where(v => v.Code.Contains(request.SearchTerm));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(v => v.IsActive == request.IsActive.Value);
            }

            if (request.Status is not null)
            {
                query = query.Where(VoucherValidityPolicy.WithStatus(request.Status, nowUtc));
            }

            var total = await query.CountAsync(cancellationToken);
            var page = Normalize(request.PageIndex, request.PageSize);
            
            var rows = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page.Page - 1) * page.PageSize)
                .Take(page.PageSize)
                .ToListAsync(cancellationToken);

            var items = mapper.Map<List<GetVouchersResponse>>(rows);
            for (var index = 0; index < rows.Count; index++)
            {
                items[index].Status = VoucherValidityPolicy.GetStatus(rows[index], nowUtc);
            }

            return new PagedResponse<GetVouchersResponse>
            {
                Items = items,
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

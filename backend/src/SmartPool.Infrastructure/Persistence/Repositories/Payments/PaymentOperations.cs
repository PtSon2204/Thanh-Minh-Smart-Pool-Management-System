using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManagePayments.DTOs;
using SmartPool.Application.Features.ManagePayments.Queries.GetAllPayments;
using SmartPool.Application.Features.ManagePayments.Queries.GetPaymentById;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Payments
{
    public sealed class PaymentOperations : IPaymentOperations
    {
        private readonly SmartPoolDbContext _context;

        public PaymentOperations(SmartPoolDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResponse<PaymentSummaryDto>> GetAllPaymentsAsync(GetAllPaymentsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Payments.AsNoTracking()
                .Include(p => p.Order)
                    .ThenInclude(o => o.User!)
                        .ThenInclude(u => u.UserProfile)
                .Include(p => p.Order)
                    .ThenInclude(o => o.Employee!)
                        .ThenInclude(e => e.User!)
                            .ThenInclude(u => u.UserProfile)
                .Include(p => p.Order)
                    .ThenInclude(o => o.OrderDetails)
                .Include(p => p.Cashier!)
                    .ThenInclude(c => c.User!)
                        .ThenInclude(u => u.UserProfile)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var statusUpper = request.Status.Trim().ToUpper();
                query = query.Where(p => p.Status != null && p.Status.ToUpper() == statusUpper);
            }

            if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
            {
                var methodUpper = request.PaymentMethod.Trim().ToUpper();
                query = query.Where(p => p.PaymentMethod.ToUpper() == methodUpper);
            }

            if (request.FromDate.HasValue)
            {
                var fromUtc = request.FromDate.Value.ToUniversalTime();
                query = query.Where(p => (p.PaymentTime ?? p.CreatedAt) >= fromUtc);
            }

            if (request.ToDate.HasValue)
            {
                var toUtc = request.ToDate.Value.ToUniversalTime();
                query = query.Where(p => (p.PaymentTime ?? p.CreatedAt) <= toUtc);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLower();
                query = query.Where(p =>
                    (p.TransactionRef != null && p.TransactionRef.ToLower().Contains(search)) ||
                    (p.Order.CustomerName != null && p.Order.CustomerName.ToLower().Contains(search)) ||
                    (p.Order.CustomerPhone != null && p.Order.CustomerPhone.Contains(search)) ||
                    (p.Order.User != null && p.Order.User.Phone != null && p.Order.User.Phone.Contains(search)) ||
                    (p.Order.User != null && p.Order.User.UserProfile != null && p.Order.User.UserProfile.FullName.ToLower().Contains(search)) ||
                    (p.Order.User != null && p.Order.User.Username != null && p.Order.User.Username.ToLower().Contains(search))
                );
            }

            var totalCount = await query.CountAsync(cancellationToken);

            int pageIndex = Math.Max(1, request.PageIndex);
            int pageSize = Math.Clamp(request.PageSize, 1, 100);

            var pagedPayments = await query
                .OrderByDescending(p => p.PaymentTime ?? p.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // Thu thập tên các loại vé / dịch vụ để hiển thị tóm tắt
            var allDetails = pagedPayments.SelectMany(p => p.Order.OrderDetails).ToList();
            var ticketTypeIds = allDetails.Where(d => d.ItemType == "TICKET").Select(d => d.ItemId).Distinct().ToList();
            var productIds = allDetails.Where(d => d.ItemType != "TICKET").Select(d => d.ItemId).Distinct().ToList();

            var ticketTypesDict = ticketTypeIds.Count > 0
                ? await _context.TicketTypes.AsNoTracking()
                    .Where(t => ticketTypeIds.Contains(t.Id))
                    .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken)
                : new Dictionary<Guid, string>();

            var productsDict = productIds.Count > 0
                ? await _context.Products.AsNoTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken)
                : new Dictionary<Guid, string>();

            // Map sang PaymentSummaryDto
            var items = pagedPayments.Select(p =>
            {
                var customerName = p.Order.User?.UserProfile?.FullName 
                    ?? p.Order.CustomerName 
                    ?? p.Order.User?.Username 
                    ?? "Khách vãng lai";

                var customerPhone = p.Order.CustomerPhone ?? p.Order.User?.Phone;

                var staffEmployee = p.Cashier ?? p.Order.Employee;
                var staffName = staffEmployee?.User?.UserProfile?.FullName 
                    ?? staffEmployee?.User?.Username;

                // Tóm tắt danh sách mặt hàng đã mua
                var summaryList = new List<string>();
                foreach (var detail in p.Order.OrderDetails)
                {
                    string name;
                    if (detail.ItemType == "TICKET" && ticketTypesDict.TryGetValue(detail.ItemId, out var tName))
                    {
                        name = tName;
                    }
                    else if (productsDict.TryGetValue(detail.ItemId, out var pName))
                    {
                        name = pName;
                    }
                    else
                    {
                        name = detail.ItemType;
                    }
                    summaryList.Add($"{name} x{detail.Quantity}");
                }

                var itemsSummary = summaryList.Count > 0 ? string.Join(", ", summaryList) : "Không có chi tiết";

                return new PaymentSummaryDto
                {
                    PaymentId = p.Id,
                    OrderId = p.OrderId,
                    InvoiceCode = $"INV-{p.Id.ToString("N")[..8].ToUpper()}",
                    PaymentTime = p.PaymentTime ?? p.CreatedAt,
                    CustomerName = customerName,
                    CustomerPhone = customerPhone,
                    ItemsSummary = itemsSummary,
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod,
                    Status = p.Status ?? "PENDING",
                    TransactionRef = p.TransactionRef,
                    StaffName = staffName
                };
            }).ToList();

            return new PagedResponse<PaymentSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<PaymentDetailDto?> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken)
        {
            var payment = await _context.Payments.AsNoTracking()
                .Include(p => p.Order)
                    .ThenInclude(o => o.User!)
                        .ThenInclude(u => u.UserProfile)
                .Include(p => p.Order)
                    .ThenInclude(o => o.Employee!)
                        .ThenInclude(e => e.User!)
                            .ThenInclude(u => u.UserProfile)
                .Include(p => p.Order)
                    .ThenInclude(o => o.Voucher)
                .Include(p => p.Order)
                    .ThenInclude(o => o.OrderDetails)
                .Include(p => p.Cashier!)
                    .ThenInclude(c => c.User!)
                        .ThenInclude(u => u.UserProfile)
                .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

            if (payment == null)
            {
                return null;
            }

            // 1. Tải thông tin các mặt hàng (TicketTypes và Products)
            var details = payment.Order.OrderDetails.ToList();
            var ticketTypeIds = details.Where(d => d.ItemType == "TICKET").Select(d => d.ItemId).Distinct().ToList();
            var productIds = details.Where(d => d.ItemType != "TICKET").Select(d => d.ItemId).Distinct().ToList();

            var ticketTypes = ticketTypeIds.Count > 0
                ? await _context.TicketTypes.AsNoTracking()
                    .Where(t => ticketTypeIds.Contains(t.Id))
                    .ToDictionaryAsync(t => t.Id, cancellationToken)
                : new Dictionary<Guid, Domain.Entities.TicketType>();

            var products = productIds.Count > 0
                ? await _context.Products.AsNoTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, cancellationToken)
                : new Dictionary<Guid, Domain.Entities.Product>();

            var orderItems = details.Select(d =>
            {
                string itemName = d.ItemType;
                string? ticketCategory = null;

                if (d.ItemType == "TICKET" && ticketTypes.TryGetValue(d.ItemId, out var tType))
                {
                    itemName = tType.Name;
                    ticketCategory = tType.TicketCategory;
                }
                else if (products.TryGetValue(d.ItemId, out var prod))
                {
                    itemName = prod.Name;
                }

                return new PaymentOrderItemDto
                {
                    Id = d.Id,
                    ItemType = d.ItemType,
                    ItemId = d.ItemId,
                    ItemName = itemName,
                    TicketCategory = ticketCategory,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice
                };
            }).ToList();

            // 2. Tải vé liên quan (nếu người dùng có vé được cấp)
            var ticketsList = new List<PaymentTicketInfoDto>();
            if (payment.Order.UserId.HasValue)
            {
                var orderCreated = payment.Order.CreatedAt ?? payment.CreatedAt ?? DateTime.UtcNow;
                var startWindow = orderCreated.AddMinutes(-5);
                var endWindow = orderCreated.AddMinutes(30);

                var tickets = await _context.Tickets.AsNoTracking()
                    .Include(t => t.TicketType)
                    .Where(t => t.UserId == payment.Order.UserId.Value
                        && t.CreatedAt >= startWindow
                        && t.CreatedAt <= endWindow)
                    .ToListAsync(cancellationToken);

                ticketsList = tickets.Select(t => new PaymentTicketInfoDto
                {
                    TicketId = t.Id,
                    QrCode = t.QrCode,
                    TicketName = t.TicketType?.Name ?? "Vé",
                    TicketCategory = t.TicketType?.TicketCategory ?? "",
                    IssueDate = t.IssueDate,
                    ExpiryDate = t.ExpiryDate,
                    Status = t.Status ?? "",
                    RemainingEntries = t.RemainingEntries
                }).ToList();
            }

            // 3. Xử lý thông tin người mua
            var customerName = payment.Order.User?.UserProfile?.FullName 
                ?? payment.Order.CustomerName 
                ?? payment.Order.User?.Username 
                ?? "Khách vãng lai";

            var customerPhone = payment.Order.CustomerPhone ?? payment.Order.User?.Phone;
            var customerEmail = payment.Order.User?.Email;
            var customerAddress = payment.Order.User?.UserProfile?.Address;

            // 4. Xử lý thông tin nhân viên xử lý (Bán vé & Thu tiền)
            var staffEmployee = payment.Cashier ?? payment.Order.Employee;
            var staffId = payment.CashierId ?? payment.Order.EmployeeId;
            var staffName = staffEmployee?.User?.UserProfile?.FullName ?? staffEmployee?.User?.Username;
            var staffPhone = staffEmployee?.User?.Phone;
            var staffEmail = staffEmployee?.User?.Email;

            return new PaymentDetailDto
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                InvoiceCode = $"INV-{payment.Id.ToString("N")[..8].ToUpper()}",
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                Status = payment.Status ?? "PENDING",
                TransactionRef = payment.TransactionRef,
                PaymentTime = payment.PaymentTime,
                CreatedAt = payment.CreatedAt,

                OrderCode = $"ORD-{payment.OrderId.ToString("N")[..8].ToUpper()}",
                TotalAmount = payment.Order.TotalAmount,
                DiscountAmount = payment.Order.DiscountAmount,
                FinalAmount = payment.Order.FinalAmount,
                OrderStatus = payment.Order.Status ?? "PENDING",
                OrderCreatedAt = payment.Order.CreatedAt,

                CustomerId = payment.Order.UserId,
                CustomerName = customerName,
                CustomerPhone = customerPhone,
                CustomerEmail = customerEmail,
                CustomerAddress = customerAddress,

                StaffId = staffId,
                StaffName = staffName,
                StaffPhone = staffPhone,
                StaffEmail = staffEmail,

                VoucherCode = payment.Order.Voucher?.Code,
                VoucherDiscountType = payment.Order.Voucher?.DiscountType,
                VoucherDiscountValue = payment.Order.Voucher?.DiscountValue,

                Items = orderItems,
                Tickets = ticketsList
            };
        }
    }
}

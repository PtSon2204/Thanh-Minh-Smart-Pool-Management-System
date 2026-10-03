using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Features.AccessControlPool.Queries.GetDailyEntrySummary;
using SmartPool.Application.Features.AccessControlPool.Queries.GetEntryHistory;
using SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories
{
    public sealed class PoolAccessOperations : IPoolAccessOperations
    {
        private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        private readonly SmartPoolDbContext _context;
        private readonly IMapper _mapper;

        public PoolAccessOperations(SmartPoolDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<LookupTicketResponse> LookupAsync(string code, DateTime utcNow, CancellationToken cancellationToken)
        {
            var ticket = await _context.Tickets.AsNoTracking()
                .Include(item => item.TicketType)
                .SingleOrDefaultAsync(item => item.QrCode == code, cancellationToken);
            if (ticket is null)
            {
                return CreateLookupResponse(false, false, "TicketNotFound", null);
            }

            var allowed = IsAllowed(ticket, utcNow, out var reason, out _);
            return CreateLookupResponse(true, allowed, reason, ticket);
        }

        public async Task<ConfirmEntryResponse> ConfirmAsync(
            string code,
            string inputMode,
            Guid operatorId,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var ticket = await _context.Tickets.FromSqlInterpolated(
                    $"SELECT * FROM smart_pool.tickets WHERE qr_code = {code} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var utcNow = DateTime.UtcNow;

            if (ticket is null)
            {
                return await SaveResultAsync(null, inputMode, operatorId, utcNow, false, "TicketNotFound", transaction, cancellationToken);
            }

            await _context.Entry(ticket).Reference(item => item.TicketType).LoadAsync(cancellationToken);

            if (!IsAllowed(ticket, utcNow, out var reason, out var category))
            {
                return await SaveResultAsync(ticket, inputMode, operatorId, utcNow, false, reason, transaction, cancellationToken);
            }

            if (category == TicketCategoryEnum.VE_THANG
                && await HasRecentAllowedEntryAsync(ticket.Id, utcNow, cancellationToken))
            {
                return await SaveResultAsync(ticket, inputMode, operatorId, utcNow, false, "DuplicateScan", transaction, cancellationToken);
            }

            if (category == TicketCategoryEnum.VE_LUOT)
            {
                ticket.Status = PoolAccessValues.Used;
            }

            return await SaveResultAsync(ticket, inputMode, operatorId, utcNow, true, "Allowed", transaction, cancellationToken);
        }

        public async Task<PagedResponse<GetEntryHistoryResponse>> GetHistoryAsync(GetEntryHistoryQuery query, CancellationToken cancellationToken)
        {
            var entryLogs = _context.EntryLogs.AsNoTracking().Where(item => item.ScanTime != null);
            if (query.Date is { } date)
            {
                entryLogs = ApplyLocalDate(entryLogs, date);
            }

            if (query.Status is not null)
            {
                entryLogs = entryLogs.Where(item => item.Status == query.Status);
            }

            if (query.TicketId is { } ticketId)
            {
                entryLogs = entryLogs.Where(item => item.TicketId == ticketId);
            }

            var totalCount = await entryLogs.CountAsync(cancellationToken);
            var items = await entryLogs.OrderByDescending(item => item.ScanTime).ThenByDescending(item => item.Id)
                .Skip(checked((int)(((long)query.PageIndex - 1) * query.PageSize)))
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResponse<GetEntryHistoryResponse>
            {
                Items = _mapper.Map<List<GetEntryHistoryResponse>>(items),
                TotalCount = totalCount,
                PageIndex = query.PageIndex,
                PageSize = query.PageSize
            };
        }

        public async Task<GetDailyEntrySummaryResponse> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken)
        {
            var acceptedEntries = await ApplyLocalDate(_context.EntryLogs.AsNoTracking(), date)
                .CountAsync(item => item.Status == PoolAccessValues.Allowed, cancellationToken);
            return new GetDailyEntrySummaryResponse
            {
                Date = date,
                AcceptedEntries = acceptedEntries
            };
        }

        private async Task<bool> HasRecentAllowedEntryAsync(Guid ticketId, DateTime utcNow, CancellationToken cancellationToken)
        {
            return await _context.EntryLogs.AnyAsync(
                item => item.TicketId == ticketId
                    && item.Status == PoolAccessValues.Allowed
                    && item.ScanTime >= utcNow.AddSeconds(-5)
                    && item.ScanTime <= utcNow,
                cancellationToken);
        }

        private async Task<ConfirmEntryResponse> SaveResultAsync(
            Ticket? ticket,
            string inputMode,
            Guid operatorId,
            DateTime utcNow,
            bool allowed,
            string reason,
            IDbContextTransaction transaction,
            CancellationToken cancellationToken)
        {
            _context.EntryLogs.Add(new EntryLog
            {
                Id = Guid.NewGuid(),
                TicketId = ticket?.Id,
                Status = allowed ? PoolAccessValues.Allowed : PoolAccessValues.Denied,
                Message = reason,
                ScanTime = utcNow,
                OperatorId = operatorId,
                InputMode = inputMode
            });
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ConfirmEntryResponse
            {
                Allowed = allowed,
                Reason = reason,
                Message = GetMessage(reason),
                Ticket = ticket is null ? null : ToTicket(ticket),
                ScanTime = utcNow,
                InputMode = inputMode
            };
        }

        private static bool IsAllowed(Ticket ticket, DateTime utcNow, out string reason, out TicketCategoryEnum category)
        {
            if (!TryGetCategory(ticket, out category))
            {
                reason = "UnsupportedCategory";
                return false;
            }

            if (ticket.Status != PoolAccessValues.Active || ticket.IsDeleted != false)
            {
                reason = "TicketInactive";
                return false;
            }

            if (ticket.TicketType.IsActive != true || ticket.TicketType.IsDeleted != false)
            {
                reason = "TicketTypeInactive";
                return false;
            }

            if (ticket.IssueDate is { } issueDate && issueDate > utcNow)
            {
                reason = "IssueDateInFuture";
                return false;
            }

            if (category == TicketCategoryEnum.VE_THANG && ticket.ExpiryDate is null)
            {
                reason = "MonthlyExpiryMissing";
                return false;
            }

            if (ticket.ExpiryDate is { } expiry && expiry <= utcNow)
            {
                reason = "TicketExpired";
                return false;
            }

            reason = "Allowed";
            return true;
        }

        private static bool TryGetCategory(Ticket ticket, out TicketCategoryEnum category)
        {
            return Enum.TryParse(ticket.TicketType.TicketCategory, false, out category)
                && Enum.IsDefined(typeof(TicketCategoryEnum), category)
                && ticket.TicketType.TicketCategory == category.ToString();
        }

        private static LookupTicketResponse CreateLookupResponse(bool found, bool isValid, string reason, Ticket? ticket)
        {
            return new LookupTicketResponse
            {
                Found = found,
                IsValid = isValid,
                Reason = reason,
                Message = GetLookupMessage(reason),
                Ticket = ticket is null ? null : ToTicket(ticket)
            };
        }

        private static TicketSummaryResponse ToTicket(Ticket ticket)
        {
            var hasCategory = TryGetCategory(ticket, out var category);
            return new TicketSummaryResponse
            {
                Id = ticket.Id,
                TicketTypeName = ticket.TicketType.Name,
                Category = hasCategory ? category : null,
                ExpiryDate = ticket.ExpiryDate,
                Status = ticket.Status,
                RemainingEntries = ticket.RemainingEntries
            };
        }

        private static string GetMessage(string reason)
        {
            return reason switch
            {
                "Allowed" => "Vé hợp lệ. Đã xác nhận lượt vào bể.",
                "TicketNotFound" => "Không tìm thấy vé.",
                "TicketInactive" => "Vé không còn hiệu lực.",
                "TicketTypeInactive" => "Loại vé hiện không hoạt động.",
                "IssueDateInFuture" => "Vé chưa đến ngày sử dụng.",
                "MonthlyExpiryMissing" => "Vé tháng thiếu ngày hết hạn.",
                "TicketExpired" => "Vé đã hết hạn.",
                "InsufficientEntries" => "Vé lượt không còn lượt vào bể.",
                "DuplicateScan" => "Vé vừa được quét. Vui lòng không quét lại ngay.",
                "UnsupportedCategory" => "Phân loại vé không được hỗ trợ.",
                _ => "Không thể xác nhận lượt vào bể."
            };
        }

        private static string GetLookupMessage(string reason)
        {
            return reason == "Allowed"
                ? "Vé hợp lệ để vào bể. Việc tra cứu không xác nhận lượt vào."
                : GetMessage(reason);
        }

        private static IQueryable<EntryLog> ApplyLocalDate(IQueryable<EntryLog> query, DateOnly date)
        {
            var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, VietnamTimeZone);
            var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), VietnamTimeZone);
            return query.Where(item => item.ScanTime >= utcStart && item.ScanTime < utcEnd);
        }
    }
}

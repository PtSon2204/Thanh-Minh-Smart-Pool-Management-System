using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.TempModels;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed class PoolAccessOperations(SmartPoolDbContext context) : IPoolAccessOperations
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<TicketLookupResult> LookupAsync(string code, DateTime utcNow, CancellationToken cancellationToken)
    {
        var ticket = await context.Tickets.AsNoTracking().Include(item => item.TicketType)
            .SingleOrDefaultAsync(item => item.QrCode == code, cancellationToken);
        if (ticket is null)
            return new TicketLookupResult(false, false, "TicketNotFound", null);

        return new TicketLookupResult(true, IsAllowed(ticket, utcNow, out var reason), reason, ToTicket(ticket));
    }

    public async Task<EntryConfirmationResult> ConfirmAsync(
        string code,
        string inputMode,
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var ticket = await context.Tickets.FromSqlInterpolated(
                $"SELECT * FROM smart_pool.tickets WHERE qr_code = {code} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var utcNow = DateTime.UtcNow;

        if (ticket is null)
            return await SaveResultAsync(null, inputMode, operatorId, utcNow, false, "TicketNotFound", transaction, cancellationToken);

        await context.Entry(ticket).Reference(item => item.TicketType).LoadAsync(cancellationToken);

        if (!IsAllowed(ticket, utcNow, out var reason))
            return await SaveResultAsync(ticket, inputMode, operatorId, utcNow, false, reason, transaction, cancellationToken);

        if (ticket.TicketType.TicketCategory == PoolAccessValues.Monthly && await HasRecentAllowedEntryAsync(ticket.Id, utcNow, cancellationToken))
            return await SaveResultAsync(ticket, inputMode, operatorId, utcNow, false, "DuplicateScan", transaction, cancellationToken);

        if (ticket.TicketType.TicketCategory == PoolAccessValues.SingleUse)
            ticket.Status = PoolAccessValues.Used;

        return await SaveResultAsync(ticket, inputMode, operatorId, utcNow, true, "Allowed", transaction, cancellationToken);
    }

    public async Task<PagedResult<EntryHistoryItemDto>> GetHistoryAsync(EntryHistoryFilter filter, CancellationToken cancellationToken)
    {
        var query = context.EntryLogs.AsNoTracking().Where(item => item.ScanTime != null);
        if (filter.Date is { } date)
            query = ApplyLocalDate(query, date);
        if (filter.Status is not null)
            query = query.Where(item => item.Status == filter.Status);
        if (filter.TicketId is { } ticketId)
            query = query.Where(item => item.TicketId == ticketId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.ScanTime).ThenByDescending(item => item.Id)
            .Skip(checked((int)(((long)filter.Page - 1) * filter.PageSize))).Take(filter.PageSize)
            .Select(item => new EntryHistoryItemDto(item.Id, item.TicketId, item.Status, item.Message,
                item.ScanTime!.Value, item.OperatorId, item.InputMode))
            .ToListAsync(cancellationToken);
        return new PagedResult<EntryHistoryItemDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<DailyEntrySummaryDto> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var count = await ApplyLocalDate(context.EntryLogs.AsNoTracking(), date)
            .CountAsync(item => item.Status == PoolAccessValues.Allowed, cancellationToken);
        return new DailyEntrySummaryDto(date, count);
    }

    private async Task<bool> HasRecentAllowedEntryAsync(Guid ticketId, DateTime utcNow, CancellationToken cancellationToken) =>
        await context.EntryLogs.AnyAsync(item => item.TicketId == ticketId && item.Status == PoolAccessValues.Allowed &&
            item.ScanTime >= utcNow.AddSeconds(-5) && item.ScanTime <= utcNow, cancellationToken);

    private async Task<EntryConfirmationResult> SaveResultAsync(
        Ticket? ticket, string inputMode, Guid operatorId, DateTime utcNow, bool allowed, string reason,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        context.EntryLogs.Add(new EntryLog
        {
            Id = Guid.NewGuid(), TicketId = ticket?.Id, Status = allowed ? PoolAccessValues.Allowed : PoolAccessValues.Denied,
            Message = reason, ScanTime = utcNow, OperatorId = operatorId, InputMode = inputMode
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new EntryConfirmationResult(allowed, reason, ticket is null ? null : ToTicket(ticket), utcNow, inputMode);
    }

    private static bool IsAllowed(Ticket ticket, DateTime utcNow, out string reason)
    {
        var category = ticket.TicketType.TicketCategory;
        if (category is not PoolAccessValues.SingleUse and not PoolAccessValues.Monthly) { reason = "UnsupportedCategory"; return false; }
        if (ticket.Status != PoolAccessValues.Active || ticket.IsDeleted != false) { reason = "TicketInactive"; return false; }
        if (ticket.TicketType.IsActive != true || ticket.TicketType.IsDeleted != false) { reason = "TicketTypeInactive"; return false; }
        if (ticket.IssueDate is { } issueDate && issueDate > utcNow) { reason = "IssueDateInFuture"; return false; }
        if (category == PoolAccessValues.Monthly && ticket.ExpiryDate is null) { reason = "MonthlyExpiryMissing"; return false; }
        if (category == PoolAccessValues.Monthly && ticket.ExpiryDate <= utcNow) { reason = "TicketExpired"; return false; }
        reason = "Allowed";
        return true;
    }

    private static TicketSummaryDto ToTicket(Ticket ticket) => new(
        ticket.Id, ticket.TicketType.Name, ticket.TicketType.TicketCategory, ticket.ExpiryDate, ticket.Status);

    private static IQueryable<EntryLog> ApplyLocalDate(IQueryable<EntryLog> query, DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, VietnamTimeZone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), VietnamTimeZone);
        return query.Where(item => item.ScanTime >= utcStart && item.ScanTime < utcEnd);
    }
}

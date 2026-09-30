using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IPoolAccessOperations
{
    Task<TicketLookupResult> LookupAsync(string code, DateTime utcNow, CancellationToken cancellationToken);

    Task<EntryConfirmationResult> ConfirmAsync(
        string code,
        string inputMode,
        Guid operatorId,
        CancellationToken cancellationToken);

    Task<PagedResult<EntryHistoryItemDto>> GetHistoryAsync(
        EntryHistoryFilter filter,
        CancellationToken cancellationToken);

    Task<DailyEntrySummaryDto> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken);
}

public sealed record EntryHistoryFilter(int Page, int PageSize, DateOnly? Date, string? Status, Guid? TicketId);

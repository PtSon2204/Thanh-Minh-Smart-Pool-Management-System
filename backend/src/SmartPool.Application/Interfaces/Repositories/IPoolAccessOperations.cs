using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry;
using SmartPool.Application.Features.AccessControlPool.Queries.GetDailyEntrySummary;
using SmartPool.Application.Features.AccessControlPool.Queries.GetEntryHistory;
using SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket;

namespace SmartPool.Application.Interfaces.Repositories
{

public interface IPoolAccessOperations
{
    Task<LookupTicketResponse> LookupAsync(string code, CancellationToken cancellationToken);

    Task<ConfirmEntryResponse> ConfirmAsync(
        string code,
        string inputMode,
        Guid operatorId,
        CancellationToken cancellationToken);

    Task<PagedResponse<GetEntryHistoryResponse>> GetHistoryAsync(
        GetEntryHistoryQuery query,
        CancellationToken cancellationToken);

    Task<GetDailyEntrySummaryResponse> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken);
}
}

using SmartPool.Application.Features.Notifications.Models;
using System.Collections.Generic;
using MediatR;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.Notifications.Queries.GetEmailHistory;

public sealed class GetEmailHistoryHandler : IRequestHandler<GetEmailHistoryQuery, IReadOnlyList<EmailHistoryItem>>
{
    private readonly IEmailHistoryStore _historyStore;

    public GetEmailHistoryHandler(IEmailHistoryStore historyStore)
    {
        _historyStore = historyStore;
    }

    public Task<IReadOnlyList<EmailHistoryItem>> Handle(GetEmailHistoryQuery request, CancellationToken cancellationToken)
    {
        return _historyStore.GetHistoryAsync(request.Limit);
    }
}

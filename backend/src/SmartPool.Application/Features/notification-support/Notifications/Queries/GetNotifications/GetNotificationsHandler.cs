using MediatR;
using System.Linq;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsHandler : IRequestHandler<GetNotificationsQuery, GetNotificationsResult>
{
    private readonly IRepository<Notification> _repo;

    public GetNotificationsHandler(IRepository<Notification> repo)
    {
        _repo = repo;
    }

    public async Task<GetNotificationsResult> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var all = await _repo.GetAllAsync(cancellationToken);

        // Lọc
        var filtered = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Type))
            filtered = filtered.Where(n => n.Type == request.Type);

        if (request.IsRead.HasValue)
            filtered = filtered.Where(n => n.IsRead == request.IsRead.Value);

        var ordered = filtered.OrderByDescending(n => n.CreatedAt).ToList();

        var totalCount = ordered.Count;
        var unreadCount = all.Count(n => n.IsRead != true);

        var items = ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new GetNotificationResponse
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Content = n.Content,
                Type = n.Type,
                IsRead = n.IsRead ?? false,
                CreatedAt = n.CreatedAt,
            })
            .ToList();

        return new GetNotificationsResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            UnreadCount = unreadCount,
        };
    }
}

using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Notifications.Commands.CreateNotification;

public sealed class CreateNotificationHandler : IRequestHandler<CreateNotificationCommand, Guid>
{
    private readonly INotificationOperations _notificationOperations;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUser _currentUser;

    public CreateNotificationHandler(
        INotificationOperations notificationOperations, 
        INotificationService notificationService,
        ICurrentUser currentUser)
    {
        _notificationOperations = notificationOperations;
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateNotificationCommand request, CancellationToken cancellationToken)
    {
        var targetUserId = request.UserId != Guid.Empty
            ? request.UserId
            : (_currentUser.UserId ?? Guid.Empty);

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            Title = request.Title,
            Content = request.Content,
            Type = request.Type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        };

        await _notificationOperations.AddAsync(notification, cancellationToken);

        try
        {
            var payload = new
            {
                id = notification.Id,
                userId = notification.UserId,
                title = notification.Title,
                content = notification.Content,
                type = notification.Type,
                isRead = false,
                createdAt = notification.CreatedAt
            };

            await _notificationService.SendToAllAsync("ReceiveNotification", payload, cancellationToken);
        }
        catch
        {
            // Log if needed
        }

        return notification.Id;
    }
}

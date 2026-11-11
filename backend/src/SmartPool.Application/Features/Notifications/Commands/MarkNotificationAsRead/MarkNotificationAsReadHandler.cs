using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadHandler : IRequestHandler<MarkNotificationAsReadCommand, bool>
{
    private readonly INotificationOperations _notificationOperations;

    public MarkNotificationAsReadHandler(INotificationOperations notificationOperations)
    {
        _notificationOperations = notificationOperations;
    }

    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notificationOperations.GetByIdAsync(request.NotificationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy thông báo với Id: {request.NotificationId}");

        notification.IsRead = true;
        await _notificationOperations.UpdateAsync(notification, cancellationToken);

        return true;
    }
}

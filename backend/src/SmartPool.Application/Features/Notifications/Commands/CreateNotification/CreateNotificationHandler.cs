using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Notifications.Commands.CreateNotification;

public sealed class CreateNotificationHandler : IRequestHandler<CreateNotificationCommand, Guid>
{
    private readonly IRepository<Notification> _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUser _currentUser;

    public CreateNotificationHandler(
        IRepository<Notification> repo, 
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
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

        await _repo.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            // Transient broadcast errors should not fail database persistence
        }

        return notification.Id;
    }
}


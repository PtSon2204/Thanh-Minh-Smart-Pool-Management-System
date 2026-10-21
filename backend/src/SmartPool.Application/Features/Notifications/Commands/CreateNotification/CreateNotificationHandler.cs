using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Notifications.Commands.CreateNotification;

public sealed class CreateNotificationHandler : IRequestHandler<CreateNotificationCommand, Guid>
{
    private readonly IRepository<Notification> _repo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateNotificationHandler(IRepository<Notification> repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = request.Title,
            Content = request.Content,
            Type = request.Type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        };

        await _repo.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return notification.Id;
    }
}

using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadHandler : IRequestHandler<MarkNotificationAsReadCommand, bool>
{
    private readonly IRepository<Notification> _repo;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationAsReadHandler(IRepository<Notification> repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _repo.GetByIdAsync(request.NotificationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy thông báo với Id: {request.NotificationId}");

        notification.IsRead = true;
        _repo.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

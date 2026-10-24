using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ActivateSwimClass;

namespace SmartPool.Application.Interfaces.Repositories;

public interface ISwimClassOperations
{
    Task<CreateSwimClassResponse> CreateAsync(
        CreateSwimClassCommand request,
        Guid createdByUserId,
        CancellationToken cancellationToken);

    Task<CreateClassSessionsResponse> CreateSessionsAsync(
        CreateClassSessionsCommand request,
        CancellationToken cancellationToken);

    Task<ActivateSwimClassResponse> ActivateAsync(
        Guid classId,
        CancellationToken cancellationToken);
}

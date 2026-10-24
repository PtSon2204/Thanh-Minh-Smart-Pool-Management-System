using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;

namespace SmartPool.Application.Interfaces.Repositories;

public interface ISwimClassOperations
{
    Task<CreateSwimClassResponse> CreateAsync(
        CreateSwimClassCommand request,
        Guid createdByUserId,
        CancellationToken cancellationToken);
}

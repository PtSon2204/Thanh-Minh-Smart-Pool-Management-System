using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ActivateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Interfaces.Repositories;

public interface ISwimClassOperations
{
    Task<PagedResponse<SwimClassListItem>> GetListAsync(
        GetSwimClassesQuery request,
        Guid userId,
        string role,
        CancellationToken cancellationToken);

    Task<SwimClassDetailResponse?> GetByIdAsync(
        Guid classId,
        Guid userId,
        string role,
        CancellationToken cancellationToken);

    Task<CreateSwimClassResponse> CreateAsync(
        CreateSwimClassCommand request,
        Guid createdByUserId,
        CancellationToken cancellationToken);

    Task<UpdateSwimClassResponse> UpdateAsync(
        UpdateSwimClassCommand request,
        CancellationToken cancellationToken);

    Task<CreateClassSessionsResponse> CreateSessionsAsync(
        CreateClassSessionsCommand request,
        CancellationToken cancellationToken);

    Task<ActivateSwimClassResponse> ActivateAsync(
        Guid classId,
        CancellationToken cancellationToken);
}

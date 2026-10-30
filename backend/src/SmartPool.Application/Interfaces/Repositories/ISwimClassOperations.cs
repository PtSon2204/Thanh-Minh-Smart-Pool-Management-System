using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ActivateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassRegistration;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMySwimClasses;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Interfaces.Repositories;

public interface ISwimClassOperations
{
    Task<PagedResponse<MySwimClassItem>> GetMyClassesAsync(
        GetMySwimClassesQuery request,
        Guid customerUserId,
        CancellationToken cancellationToken);
    Task<MyClassRegistrationSnapshot?> GetMyRegistrationAsync(
        Guid classId,
        Guid customerUserId,
        CancellationToken cancellationToken);
    Task<RegisterForSwimClassResponse> RegisterAsync(
        Guid classId,
        Guid customerUserId,
        CancellationToken cancellationToken);
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
        Guid coachUserId,
        CancellationToken cancellationToken);

    Task<UpdateClassSessionResponse> UpdateSessionAsync(
        UpdateClassSessionCommand request,
        Guid coachUserId,
        CancellationToken cancellationToken);

    Task<CreateClassSessionsResponse> CreateSessionsAsync(
        CreateClassSessionsCommand request,
        Guid coachUserId,
        CancellationToken cancellationToken);

    Task<ActivateSwimClassResponse> ActivateAsync(
        Guid classId,
        Guid coachUserId,
        CancellationToken cancellationToken);
}

using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ActivateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.DeactivateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassSession;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelMyClassRegistration;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassRegistration;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassRegistration;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMySwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrations;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassAttendances;

namespace SmartPool.Application.Interfaces.Repositories;

public interface ISwimClassOperations
{
    Task<AttendanceSubmissionContext> GetAttendanceSubmissionContextAsync(Guid classId,
        Guid sessionId, Guid customerUserId, CancellationToken cancellationToken);
    Task<ClassAttendanceResponse> SubmitAttendanceAsync(Guid classId, Guid sessionId,
        Guid customerUserId, byte[] image, string format, CancellationToken cancellationToken);
    Task<PagedResponse<MyClassAttendanceItem>> GetMyAttendancesAsync(Guid classId,
        Guid customerUserId, int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task<PagedResponse<ClassRegistrationResponse>> GetClassRegistrationsAsync(
        GetClassRegistrationsQuery request, Guid coachUserId, CancellationToken cancellationToken);
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
    Task<CancelMyClassRegistrationResponse> CancelMyRegistrationAsync(
        Guid classId, Guid customerUserId, CancellationToken cancellationToken);
    Task<CancelClassRegistrationResponse> CancelClassRegistrationAsync(
        CancelClassRegistrationCommand request, Guid coachUserId, CancellationToken cancellationToken);
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

    Task<CancelClassSessionResponse> CancelSessionAsync(
        CancelClassSessionCommand request,
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

    Task<DeactivateSwimClassResponse> DeactivateAsync(
        DeactivateSwimClassCommand request,
        Guid coachUserId,
        CancellationToken cancellationToken);
}

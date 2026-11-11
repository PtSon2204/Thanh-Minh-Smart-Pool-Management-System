using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassRegistration;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMySwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrations;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<PagedResponse<ClassRegistrationResponse>> GetClassRegistrationsAsync(
        GetClassRegistrationsQuery request, Guid coachUserId, CancellationToken cancellationToken)
    {
        var swimClass = await context.SwimClasses.AsNoTracking()
            .Where(item => item.Id == request.ClassId)
            .Select(item => new { item.CreatedByUserId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");

        if (swimClass.CreatedByUserId != coachUserId)
            throw new UnauthorizedAccessException("Chỉ Coach tạo lớp được xem danh sách đăng ký.");

        var query = context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == request.ClassId);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(item => EF.Functions.ILike(item.Status, status));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.RegisteredAt)
            .ThenByDescending(item => item.Id)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new ClassRegistrationResponse(
                item.Id,
                item.CustomerUserId,
                item.CustomerUser.UserProfile != null
                    ? item.CustomerUser.UserProfile.FullName
                    : item.CustomerUser.Username,
                item.Status,
                item.RegisteredAt,
                item.ConfirmedAt,
                item.CancelledAt,
                item.CancelledByUserId,
                item.CancelReason,
                item.ReviewedByUserId,
                item.ReviewedAt,
                item.ReviewNote,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ClassRegistrationResponse>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<PagedResponse<MySwimClassItem>> GetMyClassesAsync(
        GetMySwimClassesQuery request,
        Guid customerUserId,
        CancellationToken cancellationToken)
    {
        var query = context.ClassRegistrations.AsNoTracking()
            .Where(item => item.CustomerUserId == customerUserId);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(item => EF.Functions.ILike(item.Status, status));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var items = await query
            .OrderByDescending(item => item.RegisteredAt)
            .ThenByDescending(item => item.Id)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new MySwimClassItem(
                item.Id,
                item.ClassId,
                item.Class.Name,
                item.Class.Status,
                item.Class.CoachUser.UserProfile != null
                    ? item.Class.CoachUser.UserProfile.FullName
                    : item.Class.CoachUser.Username,
                item.Class.Sessions.Count(),
                item.Class.Sessions
                    .Where(session => session.Status == "Scheduled" && session.StartsAt > now)
                    .OrderBy(session => session.StartsAt)
                    .Select(session => (DateTime?)session.StartsAt)
                    .FirstOrDefault(),
                item.Status,
                item.RegisteredAt,
                item.ConfirmedAt,
                item.CancelledAt,
                item.Status == "PendingApproval" ||
                (item.Status == "Confirmed" &&
                 !item.Class.Sessions.Any(session =>
                    session.Status == "Scheduled" && session.StartsAt <= now)),
                item.ReviewedAt,
                item.ReviewNote,
                item.CancelReason,
                item.Status == "Rejected" && item.Class.Status == "Active" &&
                item.Class.RegistrationStartAt <= now && now < item.Class.RegistrationEndAt &&
                item.Class.CreatedByUserId != customerUserId &&
                !item.Class.Sessions.Any(session => session.Status == "Scheduled" &&
                    session.CoachUserId == customerUserId) &&
                item.Class.Sessions.Any(session => session.Status == "Scheduled" &&
                    session.StartsAt > now) &&
                item.Class.Registrations.Count(registration => registration.Status == "Confirmed") <
                    item.Class.Capacity))
            .ToListAsync(cancellationToken);

        return new PagedResponse<MySwimClassItem>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public Task<MyClassRegistrationSnapshot?> GetMyRegistrationAsync(
        Guid classId,
        Guid customerUserId,
        CancellationToken cancellationToken) =>
        context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == classId && item.CustomerUserId == customerUserId)
            .Select(item => new MyClassRegistrationSnapshot(
                item.Id, item.ClassId, item.CustomerUserId, item.Status,
                item.RegisteredAt, item.ConfirmedAt, item.CancelledAt,
                item.ReviewedAt, item.ReviewNote, item.CancelReason))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<SwimClassListItem>> GetListAsync(
        GetSwimClassesQuery request,
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        var query = VisibleTo(userId, role);

        var name = request.SearchTerm?.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            var pattern = $"%{name.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(swimClass => EF.Functions.ILike(swimClass.Name, pattern, "\\"));
        }

        var status = request.Status?.Trim();
        if (!string.IsNullOrEmpty(status))
        {
            var statusPattern = status.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(swimClass => EF.Functions.ILike(swimClass.Status, statusPattern, "\\"));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(swimClass => swimClass.CreatedAt)
            .ThenByDescending(swimClass => swimClass.Id)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(swimClass => new SwimClassListItem(
                swimClass.Id,
                swimClass.Name,
                swimClass.Description,
                swimClass.Capacity,
                swimClass.Fee,
                swimClass.CoachUserId,
                swimClass.CoachUser.UserProfile != null
                    ? swimClass.CoachUser.UserProfile.FullName
                    : swimClass.CoachUser.Username,
                swimClass.RegistrationStartAt,
                swimClass.RegistrationEndAt,
                swimClass.Status,
                swimClass.Sessions.Count(),
                swimClass.Registrations.Count(registration => registration.Status == "Confirmed"),
                swimClass.CreatedAt,
                swimClass.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<SwimClassListItem>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<SwimClassDetailResponse?> GetByIdAsync(
        Guid classId,
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        var swimClass = await VisibleTo(userId, role)
            .Where(item => item.Id == classId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Description,
                item.Capacity,
                item.Fee,
                item.CoachUserId,
                CoachName = item.CoachUser.UserProfile != null
                    ? item.CoachUser.UserProfile.FullName
                    : item.CoachUser.Username,
                item.RegistrationStartAt,
                item.RegistrationEndAt,
                item.Status,
                item.CreatedByUserId,
                item.InactiveReason,
                item.InactivatedAt,
                ConfirmedStudentCount = item.Registrations.Count(
                    registration => registration.Status == "Confirmed"),
                item.CreatedAt,
                item.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (swimClass is null) return null;

        var sessions = await context.ClassSessions.AsNoTracking()
            .Where(session => session.ClassId == classId)
            .OrderBy(session => session.StartsAt)
            .ThenBy(session => session.Id)
            .Select(session => new SwimClassSessionDetail(
                session.Id,
                session.CoachUserId,
                session.CoachUser.UserProfile != null
                    ? session.CoachUser.UserProfile.FullName
                    : session.CoachUser.Username,
                session.StartsAt,
                session.EndsAt,
                session.Status,
                session.CancelReason,
                session.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new SwimClassDetailResponse(
            swimClass.Id,
            swimClass.Name,
            swimClass.Description,
            swimClass.Capacity,
            swimClass.Fee,
            swimClass.CoachUserId,
            swimClass.CoachName,
            swimClass.RegistrationStartAt,
            swimClass.RegistrationEndAt,
            swimClass.Status,
            swimClass.CreatedByUserId,
            swimClass.ConfirmedStudentCount,
            swimClass.CreatedAt,
            swimClass.UpdatedAt,
            sessions,
            swimClass.InactiveReason,
            swimClass.InactivatedAt);
    }

    private IQueryable<SwimClass> VisibleTo(Guid userId, string role)
    {
        var query = context.SwimClasses.AsNoTracking();
        return role switch
        {
            "CUSTOMER" => query.Where(swimClass => swimClass.Status == "Active"),
            "COACH" => query.Where(swimClass =>
                swimClass.CreatedByUserId == userId ||
                (swimClass.Status == "Active" &&
                 swimClass.Sessions.Any(session => session.CoachUserId == userId))),
            _ => throw new UnauthorizedAccessException("Không có quyền xem lớp dạy bơi.")
        };
    }
}

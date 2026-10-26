using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
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
            sessions);
    }

    private IQueryable<SwimClass> VisibleTo(Guid userId, string role)
    {
        var query = context.SwimClasses.AsNoTracking();
        return role switch
        {
            "ADMIN" => query,
            "CUSTOMER" => query.Where(swimClass => swimClass.Status == "Active"),
            "COACH" => query.Where(swimClass =>
                swimClass.Sessions.Any(session => session.CoachUserId == userId)),
            _ => throw new UnauthorizedAccessException("Không có quyền xem lớp dạy bơi.")
        };
    }
}

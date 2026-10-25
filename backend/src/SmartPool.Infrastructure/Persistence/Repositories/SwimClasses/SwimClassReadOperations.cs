using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<PagedResponse<SwimClassListItem>> GetListAsync(
        GetSwimClassesQuery request,
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        var query = context.SwimClasses.AsNoTracking().AsQueryable();

        query = role switch
        {
            "ADMIN" => query,
            "CUSTOMER" => query.Where(swimClass => swimClass.Status == "Active"),
            "COACH" => query.Where(swimClass =>
                swimClass.Sessions.Any(session => session.CoachUserId == userId)),
            _ => throw new UnauthorizedAccessException("Không có quyền xem danh sách lớp dạy bơi.")
        };

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
}

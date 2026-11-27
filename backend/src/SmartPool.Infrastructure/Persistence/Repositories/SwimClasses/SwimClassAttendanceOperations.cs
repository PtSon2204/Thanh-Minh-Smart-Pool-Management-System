using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassAttendances;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<PagedResponse<MyClassAttendanceItem>> GetMyAttendancesAsync(Guid classId,
        Guid customerUserId, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var registration = await context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == classId && item.CustomerUserId == customerUserId)
            .Select(item => new { item.Id, item.Status, item.ConfirmedAt })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Bạn chưa đăng ký lớp này.");

        var sessions = context.ClassSessions.AsNoTracking()
            .Where(item => item.ClassId == classId);
        var totalCount = await sessions.CountAsync(cancellationToken);
        var page = await sessions
            .OrderByDescending(item => item.StartsAt)
            .ThenByDescending(item => item.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new { item.Id, item.StartsAt, item.EndsAt, item.Status })
            .ToListAsync(cancellationToken);
        var sessionIds = page.Select(item => item.Id).ToArray();
        var attendanceBySession = await context.ClassAttendances.AsNoTracking()
            .Where(item => item.RegistrationId == registration.Id &&
                sessionIds.Contains(item.SessionId))
            .ToDictionaryAsync(item => item.SessionId, cancellationToken);

        return new PagedResponse<MyClassAttendanceItem>
        {
            Items = page.Select(session => new MyClassAttendanceItem(
                session.Id, session.StartsAt, session.EndsAt, session.Status,
                registration.Status,
                attendanceBySession.ContainsKey(session.Id) ||
                    (registration.Status == "Confirmed" &&
                     (registration.ConfirmedAt is null || registration.ConfirmedAt <= session.EndsAt)),
                attendanceBySession.TryGetValue(session.Id, out var attendance)
                    ? ToAttendanceResponse(attendance, classId)
                    : null)).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    private static ClassAttendanceResponse ToAttendanceResponse(ClassAttendance item, Guid classId) =>
        new(item.Id, classId, item.SessionId, item.RegistrationId,
            item.ImageUrl, item.Status, item.SubmittedAt, item.UpdatedAt, item.ProgressNote);
}

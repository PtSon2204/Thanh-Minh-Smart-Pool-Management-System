using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassSessionAttendances;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<PagedResponse<ClassSessionAttendanceItem>> GetClassSessionAttendancesAsync(
        GetClassSessionAttendancesQuery request, Guid coachUserId,
        CancellationToken cancellationToken)
    {
        var assignedSession = await context.ClassSessions.AsNoTracking()
            .Where(item => item.Id == request.SessionId && item.ClassId == request.ClassId)
            .Select(item => new { item.CoachUserId, item.EndsAt })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy buổi học của lớp.");
        if (assignedSession.CoachUserId != coachUserId)
            throw new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được xem điểm danh.");

        var query = context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == request.ClassId && item.Status == "Confirmed" &&
                (item.ConfirmedAt == null || item.ConfirmedAt <= assignedSession.EndsAt));
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = status.Equals("NotMarked", StringComparison.OrdinalIgnoreCase)
                ? query.Where(item => !context.ClassAttendances.Any(attendance =>
                    attendance.SessionId == request.SessionId && attendance.RegistrationId == item.Id))
                : query.Where(item => context.ClassAttendances.Any(attendance =>
                    attendance.SessionId == request.SessionId && attendance.RegistrationId == item.Id &&
                    EF.Functions.ILike(attendance.Status, status)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var registrations = await query
            .OrderBy(item => item.CustomerUser.UserProfile != null
                ? item.CustomerUser.UserProfile.FullName : item.CustomerUser.Username)
            .ThenByDescending(item => item.Id)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new { item.Id, item.CustomerUserId,
                CustomerName = item.CustomerUser.UserProfile != null
                    ? item.CustomerUser.UserProfile.FullName : item.CustomerUser.Username })
            .ToListAsync(cancellationToken);
        var registrationIds = registrations.Select(item => item.Id).ToArray();
        var attendanceByRegistration = await context.ClassAttendances.AsNoTracking()
            .Where(item => item.SessionId == request.SessionId && registrationIds.Contains(item.RegistrationId))
            .ToDictionaryAsync(item => item.RegistrationId, cancellationToken);
        var items = registrations.Select(item =>
        {
            attendanceByRegistration.TryGetValue(item.Id, out var attendance);
            return new ClassSessionAttendanceItem(attendance?.Id, item.Id, item.CustomerUserId,
                item.CustomerName ?? "Học viên", attendance?.ImageUrl, attendance?.Status ?? "NotMarked",
                attendance?.SubmittedAt, attendance?.UpdatedAt);
        }).ToList();

        return new PagedResponse<ClassSessionAttendanceItem>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

}

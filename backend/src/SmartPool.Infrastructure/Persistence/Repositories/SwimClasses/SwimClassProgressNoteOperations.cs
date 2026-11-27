using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassProgressNote;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<ClassAttendanceResponse> UpdateClassProgressNoteAsync(
        UpdateClassProgressNoteCommand request, Guid coachUserId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(request.ClassId, cancellationToken);

        var session = await context.ClassSessions.AsNoTracking()
            .Where(item => item.Id == request.SessionId && item.ClassId == request.ClassId)
            .Select(item => new { item.CoachUserId, item.EndsAt, item.Status })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy buổi học của lớp.");
        if (session.CoachUserId != coachUserId)
            throw new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được ghi nhận xét.");
        if (session.Status != "Scheduled" || timeProvider.GetUtcNow().UtcDateTime < session.EndsAt)
            throw new SwimClassStateConflictException("Chỉ ghi nhận xét sau khi buổi học kết thúc.");

        var attendance = await context.ClassAttendances
            .Where(item => item.SessionId == request.SessionId &&
                item.RegistrationId == request.RegistrationId &&
                item.Registration.ClassId == request.ClassId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Học viên chưa được điểm danh trong buổi này.");
        if (attendance.UpdatedAt != request.ExpectedUpdatedAt?.UtcDateTime)
            throw new SwimClassStateConflictException("Điểm danh đã thay đổi. Vui lòng tải lại danh sách.");

        attendance.ProgressNote = request.ProgressNote;
        attendance.UpdatedAt = NextVersionTimestamp(timeProvider.GetUtcNow().UtcDateTime, attendance.UpdatedAt);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToAttendanceResponse(attendance, request.ClassId);
    }
}

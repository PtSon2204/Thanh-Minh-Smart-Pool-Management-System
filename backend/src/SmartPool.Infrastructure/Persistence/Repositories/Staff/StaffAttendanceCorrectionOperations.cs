using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Features.ManageStaffs.Queries.GetAttendanceHistory;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    public async Task<RecordAttendanceResponse> CorrectAttendanceAsync(ManualAttendanceCorrectionCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.employee_shifts WHERE id = {request.AssignmentId} FOR UPDATE",
            cancellationToken);
        var assignment = await GetAssignmentAsync(request.AssignmentId, cancellationToken);
        if (assignment is null)
            throw new KeyNotFoundException("Không tìm thấy lịch làm việc.");
        if (assignment.Status == "Cancelled")
            throw new StaffConflictException("Không thể điều chỉnh chấm công cho phân công đã hủy.");
        if (assignment.Status is not ("Scheduled" or "Present" or "Completed"))
            throw new StaffConflictException("Trạng thái phân công không cho phép điều chỉnh chấm công.");

        var now = timeProvider.GetUtcNow();
        var today = ManualAttendanceCorrectionPolicy.VietnamDate(now);
        if (!ManualAttendanceCorrectionPolicy.IsWithinLookback(assignment.WorkDate, today, attendanceQrSettings.ManualLookbackDays))
            throw new StaffConflictException("Chỉ được bổ sung hoặc điều chỉnh chấm công trong 7 ngày gần nhất.");

        var expectedUpdatedAtUtc = request.ExpectedUpdatedAtUtc.UtcDateTime;
        if (assignment.UpdatedAt is not { } savedUpdatedAt || savedUpdatedAt != expectedUpdatedAtUtc)
            throw new StaffConflictException("Lịch đã thay đổi. Hãy làm mới trước khi điều chỉnh.");

        var checkInTime = assignment.CheckInTime;
        if (request.UpdateCheckIn)
        {
            var enteredCheckIn = request.CheckInTime ?? throw new StaffConflictException("Phải cung cấp giờ vào cần điều chỉnh.");
            if (!ManualAttendanceCorrectionPolicy.IsNotFuture(enteredCheckIn, now))
                throw new StaffConflictException("Giờ vào không được nằm trong tương lai.");
            checkInTime = enteredCheckIn.UtcDateTime;
        }

        var checkOutTime = assignment.CheckOutTime;
        if (request.UpdateCheckOut)
        {
            var enteredCheckOut = request.CheckOutTime ?? throw new StaffConflictException("Phải cung cấp giờ ra cần điều chỉnh.");
            if (!ManualAttendanceCorrectionPolicy.IsNotFuture(enteredCheckOut, now))
                throw new StaffConflictException("Giờ ra không được nằm trong tương lai.");
            checkOutTime = enteredCheckOut.UtcDateTime;
        }
        if (checkInTime is null && checkOutTime is not null)
            throw new StaffConflictException("Phải có giờ vào trước khi ghi nhận giờ ra.");
        if (checkInTime is not null && checkOutTime is not null && checkOutTime <= checkInTime)
            throw new StaffConflictException("Giờ ra phải sau giờ vào.");

        var checkInMethod = request.UpdateCheckIn ? "Manual" : assignment.CheckInMethod;
        var checkOutMethod = request.UpdateCheckOut ? "Manual" : assignment.CheckOutMethod;
        var status = checkOutTime is not null ? "Completed" : checkInTime is not null ? "Present" : "Scheduled";
        var changed = await context.EmployeeShifts
            .Where(item => item.Id == request.AssignmentId && item.Status == assignment.Status && item.Status != "Cancelled" &&
                item.UpdatedAt == assignment.UpdatedAt && item.CheckInTime == assignment.CheckInTime && item.CheckOutTime == assignment.CheckOutTime &&
                item.CheckInMethod == assignment.CheckInMethod && item.CheckOutMethod == assignment.CheckOutMethod)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.CheckInTime, checkInTime)
                .SetProperty(item => item.CheckOutTime, checkOutTime)
                .SetProperty(item => item.CheckInMethod, checkInMethod)
                .SetProperty(item => item.CheckOutMethod, checkOutMethod)
                .SetProperty(item => item.Status, status)
                .SetProperty(item => item.UpdatedAt, now.UtcDateTime), cancellationToken);
        if (changed == 0)
            throw new StaffConflictException("Lịch đã thay đổi. Hãy làm mới trước khi điều chỉnh.");

        var reason = request.Reason?.Trim();

        context.AttendanceLogs.Add(new AttendanceLog
        {
            Id = Guid.NewGuid(),
            EmployeeShiftId = assignment.Id,
            Action = "Correction",
            Method = "Manual",
            PerformedByUserId = request.PerformedByUserId,
            PerformedAtUtc = now.UtcDateTime,
            Reason = reason,
            BeforeCheckInTime = assignment.CheckInTime,
            BeforeCheckOutTime = assignment.CheckOutTime,
            BeforeCheckInMethod = assignment.CheckInMethod,
            BeforeCheckOutMethod = assignment.CheckOutMethod,
            BeforeStatus = assignment.Status,
            AfterCheckInTime = checkInTime,
            AfterCheckOutTime = checkOutTime,
            AfterCheckInMethod = checkInMethod,
            AfterCheckOutMethod = checkOutMethod,
            AfterStatus = status
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        assignment.CheckInTime = checkInTime;
        assignment.CheckOutTime = checkOutTime;
        assignment.CheckInMethod = checkInMethod;
        assignment.CheckOutMethod = checkOutMethod;
        assignment.Status = status;
        assignment.UpdatedAt = now.UtcDateTime;
        return mapper.Map<RecordAttendanceResponse>(assignment);
    }

    public async Task<PagedResponse<GetAttendanceHistoryResponse>> GetAttendanceHistoryAsync(GetAttendanceHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await context.EmployeeShifts.AsNoTracking().AnyAsync(item => item.Id == query.AssignmentId, cancellationToken))
            throw new KeyNotFoundException("Không tìm thấy lịch làm việc.");

        var rows = context.AttendanceLogs.AsNoTracking()
            .Include(item => item.PerformedByUser)
            .Where(item => item.EmployeeShiftId == query.AssignmentId);
        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.PerformedAtUtc).ThenByDescending(item => item.Id)
            .Skip(checked((query.PageIndex - 1) * query.PageSize))
            .Take(query.PageSize)
            .Select(item => new GetAttendanceHistoryResponse
            {
                Id = item.Id,
                Action = item.Action,
                Method = item.Method,
                PerformedByUserId = item.PerformedByUserId,
                PerformedByUsername = item.PerformedByUser.Username ?? string.Empty,
                PerformedAtUtc = item.PerformedAtUtc,
                Reason = item.Reason,
                BeforeCheckInTime = item.BeforeCheckInTime,
                BeforeCheckOutTime = item.BeforeCheckOutTime,
                BeforeCheckInMethod = item.BeforeCheckInMethod,
                BeforeCheckOutMethod = item.BeforeCheckOutMethod,
                BeforeStatus = item.BeforeStatus,
                AfterCheckInTime = item.AfterCheckInTime,
                AfterCheckOutTime = item.AfterCheckOutTime,
                AfterCheckInMethod = item.AfterCheckInMethod,
                AfterCheckOutMethod = item.AfterCheckOutMethod,
                AfterStatus = item.AfterStatus
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<GetAttendanceHistoryResponse>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }
}

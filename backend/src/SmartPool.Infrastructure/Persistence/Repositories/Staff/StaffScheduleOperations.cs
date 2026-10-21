using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories
{

public sealed partial class StaffOperations
{
    public async Task<PagedResponse<GetScheduleResponse>> GetScheduleAsync(GetScheduleQuery request, CancellationToken cancellationToken)
    {
        if (request.RequireEmployee && (request.EmployeeId is null || !await context.Employees.AsNoTracking().AnyAsync(item => item.UserId == request.EmployeeId, cancellationToken)))
            throw new StaffForbiddenException("Bạn không có quyền xem lịch làm việc này.");
        var query = context.EmployeeShifts.AsNoTracking().Include(item => item.Shift).Include(item => item.Employee).ThenInclude(employee => employee.User).ThenInclude(user => user.UserProfile).AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(item => item.EmployeeId == request.EmployeeId);
        if (request.Date is not null) query = query.Where(item => item.WorkDate == request.Date);
        if (request.FromDate is not null) query = query.Where(item => item.WorkDate >= request.FromDate);
        if (request.ToDate is not null) query = query.Where(item => item.WorkDate <= request.ToDate);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(item => item.Status == request.Status);
        var total = await query.CountAsync(cancellationToken); var page = Normalize(request.PageIndex, request.PageSize);
        var rows = await query.OrderBy(item => item.WorkDate).ThenBy(item => item.Shift.StartTime).ThenBy(item => item.EmployeeId).ThenBy(item => item.ShiftId).ThenBy(item => item.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<GetScheduleResponse> { Items = mapper.Map<List<GetScheduleResponse>>(rows), TotalCount = total, PageIndex = page.Page, PageSize = page.PageSize };
    }

    public async Task<AssignShiftResponse> AssignShiftAsync(AssignShiftCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockEmployeesAsync([request.EmployeeId], cancellationToken);
        var employee = await EligibleEmployees().AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == request.EmployeeId, cancellationToken);
        if (employee is null) throw new KeyNotFoundException("Không tìm thấy nhân viên đang làm việc.");
        var shift = await context.Shifts.FirstOrDefaultAsync(item => item.Id == request.ShiftId && item.IsActive == true, cancellationToken);
        if (shift is null) throw new KeyNotFoundException("Không tìm thấy ca làm việc đang hoạt động.");
        var assignments = await context.EmployeeShifts.Include(item => item.Shift).Where(item => item.EmployeeId == request.EmployeeId && item.WorkDate >= request.WorkDate.AddDays(-1) && item.WorkDate <= request.WorkDate.AddDays(1) && item.Status != "Cancelled").ToListAsync(cancellationToken);
        if (assignments.Any(item => Overlaps(request.WorkDate, shift, item.WorkDate, item.Shift))) throw new StaffConflictException("Ca làm việc bị trùng với lịch đã phân công.");
        var assignment = new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = request.EmployeeId, ShiftId = request.ShiftId, WorkDate = request.WorkDate, Status = "Scheduled", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Shift = shift };
        context.EmployeeShifts.Add(assignment);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsExactTupleViolation(exception))
        {
            throw new StaffConflictException("Phân công này đã tồn tại.");
        }
        await transaction.CommitAsync(cancellationToken);
        return mapper.Map<AssignShiftResponse>(assignment);
    }

    public async Task<CancelShiftAssignmentResponse> CancelAssignmentAsync(CancelShiftAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await GetAssignmentAsync(request.Id, cancellationToken);
        if (assignment is null) throw new KeyNotFoundException("Không tìm thấy lịch làm việc.");
        if (assignment.Status != "Scheduled" || assignment.CheckInTime is not null || assignment.CheckOutTime is not null) throw new StaffConflictException("Chỉ có thể hủy lịch chưa chấm công.");
        var now = DateTime.UtcNow;
        var changed = await context.EmployeeShifts.Where(item => item.Id == request.Id && item.Status == "Scheduled" && item.CheckInTime == null && item.CheckOutTime == null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Cancelled").SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (changed == 0) throw new StaffConflictException("Chỉ có thể hủy lịch đang được lên kế hoạch.");
        assignment.Status = "Cancelled";
        assignment.UpdatedAt = now;
        return mapper.Map<CancelShiftAssignmentResponse>(assignment);
    }

    public async Task<RecordAttendanceResponse> RecordAttendanceAsync(RecordAttendanceCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var assignment = await GetAssignmentAsync(request.Id, cancellationToken);
        if (assignment is null) throw new KeyNotFoundException("Không tìm thấy lịch làm việc.");
        if (request.OwnerId is not null && assignment.EmployeeId != request.OwnerId) throw new StaffForbiddenException("Bạn không có quyền chấm công cho lịch này.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (request.CheckIn && !StaffAttendanceTiming.CanCheckIn(assignment, now, attendanceQrSettings.CheckInEarlyMinutes))
            throw new StaffConflictException("Chỉ có thể check-in từ 20 phút trước giờ bắt đầu đến trước giờ kết thúc ca.");
        if (!request.CheckIn && !StaffAttendanceTiming.CanCheckOut(assignment, now))
            throw new StaffConflictException("Chỉ có thể check-out từ giờ kết thúc ca sau khi đã check-in.");
        var changed = request.CheckIn
            ? await context.EmployeeShifts.Where(item => item.Id == request.Id && item.Status == "Scheduled" && item.CheckInTime == null && item.CheckOutTime == null && item.Employee.Status == "Working" && item.Employee.User.IsDeleted != true && item.Employee.User.Status != null && EF.Functions.ILike(item.Employee.User.Status, "ACTIVE")).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Present").SetProperty(item => item.CheckInTime, now).SetProperty(item => item.CheckInMethod, request.Method).SetProperty(item => item.UpdatedAt, now), cancellationToken)
            : await context.EmployeeShifts.Where(item => item.Id == request.Id && item.Status == "Present" && item.CheckInTime != null && item.CheckOutTime == null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Completed").SetProperty(item => item.CheckOutTime, now).SetProperty(item => item.CheckOutMethod, request.Method).SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (changed == 0) throw new StaffConflictException(request.CheckIn ? "Không thể check-in lịch này." : "Cần check-in trước khi check-out.");
        context.AttendanceLogs.Add(new AttendanceLog
        {
            Id = Guid.NewGuid(),
            EmployeeShiftId = assignment.Id,
            Action = request.CheckIn ? "CheckIn" : "CheckOut",
            Method = request.Method,
            PerformedByUserId = request.PerformedByUserId,
            PerformedAtUtc = now,
            Reason = request.Method == "Manual" ? request.Reason!.Trim() : null,
            BeforeCheckInTime = assignment.CheckInTime,
            BeforeCheckOutTime = assignment.CheckOutTime,
            BeforeCheckInMethod = assignment.CheckInMethod,
            BeforeCheckOutMethod = assignment.CheckOutMethod,
            BeforeStatus = assignment.Status,
            AfterCheckInTime = request.CheckIn ? now : assignment.CheckInTime,
            AfterCheckOutTime = request.CheckIn ? assignment.CheckOutTime : now,
            AfterCheckInMethod = request.CheckIn ? request.Method : assignment.CheckInMethod,
            AfterCheckOutMethod = request.CheckIn ? assignment.CheckOutMethod : request.Method,
            AfterStatus = request.CheckIn ? "Present" : "Completed"
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return mapper.Map<RecordAttendanceResponse>((await GetAssignmentAsync(request.Id, cancellationToken))!);
    }

    private async Task<EmployeeShift?> GetAssignmentAsync(Guid id, CancellationToken cancellationToken) => await context.EmployeeShifts.AsNoTracking().Include(item => item.Shift).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    private IQueryable<Employee> EligibleEmployees() => context.Employees.Where(item =>
        item.Status == "Working" && item.User.IsDeleted != true && item.User.Status != null &&
        EF.Functions.ILike(item.User.Status, "ACTIVE"));
    private async Task LockEmployeesAsync(IEnumerable<Guid> employeeIds, CancellationToken cancellationToken)
    {
        foreach (var employeeId in employeeIds.Distinct().Order())
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({employeeId.ToString()}, 0))", cancellationToken);
    }
    private static bool IsExactTupleViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException &&
        postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
        postgresException.ConstraintName == "ux_employee_shifts_employee_id_shift_id_work_date";
    private static bool Overlaps(DateOnly firstDate, Shift firstShift, DateOnly secondDate, Shift secondShift) { var first = StaffAttendanceTiming.Interval(firstDate, firstShift); var second = StaffAttendanceTiming.Interval(secondDate, secondShift); return first.Start < second.End && second.Start < first.End; }
}

public static class StaffAttendanceTiming
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static bool CanCheckIn(EmployeeShift assignment, DateTime utcNow, int checkInEarlyMinutes)
    {
        var (scheduledStart, scheduledEnd) = Interval(assignment.WorkDate, assignment.Shift);
        return utcNow >= scheduledStart.AddMinutes(-checkInEarlyMinutes) && utcNow < scheduledEnd;
    }

    public static bool CanCheckOut(EmployeeShift assignment, DateTime utcNow)
    {
        var (_, scheduledEnd) = Interval(assignment.WorkDate, assignment.Shift);
        return utcNow >= scheduledEnd && assignment.CheckInTime is not null && assignment.CheckInTime < utcNow;
    }

    public static (DateTime Start, DateTime End) Interval(DateOnly date, Shift shift)
    {
        var start = date.ToDateTime(shift.StartTime);
        var end = date.ToDateTime(shift.EndTime).AddDays(shift.EndTime < shift.StartTime ? 1 : 0);
        return (TimeZoneInfo.ConvertTimeToUtc(start, VietnamTimeZone), TimeZoneInfo.ConvertTimeToUtc(end, VietnamTimeZone));
    }
}
}

using Microsoft.EntityFrameworkCore;
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
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    public async Task<PagedResponse<GetScheduleResponse>> GetScheduleAsync(GetScheduleQuery request, CancellationToken cancellationToken)
    {
        if (request.RequireEmployee && (request.EmployeeId is null || !await context.Employees.AsNoTracking().AnyAsync(item => item.UserId == request.EmployeeId, cancellationToken)))
            throw new StaffForbiddenException("Bạn không có quyền xem lịch làm việc này.");
        var query = context.EmployeeShifts.AsNoTracking().Include(item => item.Shift).AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(item => item.EmployeeId == request.EmployeeId);
        if (request.Date is not null) query = query.Where(item => item.WorkDate == request.Date);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(item => item.Status == request.Status);
        var total = await query.CountAsync(cancellationToken); var page = Normalize(request.PageIndex, request.PageSize);
        var rows = await query.OrderBy(item => item.WorkDate).ThenBy(item => item.Shift.StartTime).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<GetScheduleResponse> { Items = mapper.Map<List<GetScheduleResponse>>(rows), TotalCount = total, PageIndex = page.Page, PageSize = page.PageSize };
    }

    public async Task<AssignShiftResponse> AssignShiftAsync(AssignShiftCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({request.EmployeeId.ToString()}, 0))", cancellationToken);
        var employee = await context.Employees.AsNoTracking().FirstOrDefaultAsync(item =>
            item.UserId == request.EmployeeId && item.Status == "Working" &&
            item.User.IsDeleted != true && item.User.Status != null &&
            EF.Functions.ILike(item.User.Status, "ACTIVE"), cancellationToken);
        if (employee is null) throw new KeyNotFoundException("Không tìm thấy nhân viên đang làm việc.");
        var shift = await context.Shifts.FirstOrDefaultAsync(item => item.Id == request.ShiftId && item.IsActive == true, cancellationToken);
        if (shift is null) throw new KeyNotFoundException("Không tìm thấy ca làm việc đang hoạt động.");
        var assignments = await context.EmployeeShifts.Include(item => item.Shift).Where(item => item.EmployeeId == request.EmployeeId && item.WorkDate >= request.WorkDate.AddDays(-1) && item.WorkDate <= request.WorkDate.AddDays(1) && item.Status != "Cancelled").ToListAsync(cancellationToken);
        if (assignments.Any(item => Overlaps(request.WorkDate, shift, item.WorkDate, item.Shift))) throw new StaffConflictException("Ca làm việc bị trùng với lịch đã phân công.");
        var assignment = new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = request.EmployeeId, ShiftId = request.ShiftId, WorkDate = request.WorkDate, Status = "Scheduled", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Shift = shift };
        context.EmployeeShifts.Add(assignment); await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return mapper.Map<AssignShiftResponse>(assignment);
    }

    public async Task<CancelShiftAssignmentResponse> CancelAssignmentAsync(CancelShiftAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await GetAssignmentAsync(request.Id, cancellationToken);
        if (assignment is null) throw new KeyNotFoundException("Không tìm thấy lịch làm việc.");
        if (assignment.Status != "Scheduled") throw new StaffConflictException("Chỉ có thể hủy lịch đang được lên kế hoạch.");
        var now = DateTime.UtcNow;
        var changed = await context.EmployeeShifts.Where(item => item.Id == request.Id && item.Status == "Scheduled").ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Cancelled").SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (changed == 0) throw new StaffConflictException("Chỉ có thể hủy lịch đang được lên kế hoạch.");
        assignment.Status = "Cancelled";
        assignment.UpdatedAt = now;
        return mapper.Map<CancelShiftAssignmentResponse>(assignment);
    }

    public async Task<RecordAttendanceResponse> RecordAttendanceAsync(RecordAttendanceCommand request, CancellationToken cancellationToken)
    {
        var assignment = await GetAssignmentAsync(request.Id, cancellationToken);
        if (assignment is null) throw new KeyNotFoundException("Không tìm thấy lịch làm việc.");
        if (request.OwnerId is not null && assignment.EmployeeId != request.OwnerId) throw new StaffForbiddenException("Bạn không có quyền chấm công cho lịch này.");
        var now = DateTime.UtcNow;
        var changed = request.CheckIn
            ? await context.EmployeeShifts.Where(item => item.Id == request.Id && item.Status == "Scheduled" && item.CheckInTime == null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Present").SetProperty(item => item.CheckInTime, now).SetProperty(item => item.UpdatedAt, now), cancellationToken)
            : await context.EmployeeShifts.Where(item => item.Id == request.Id && item.Status == "Present" && item.CheckInTime != null && item.CheckOutTime == null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Completed").SetProperty(item => item.CheckOutTime, now).SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (changed == 0) throw new StaffConflictException(request.CheckIn ? "Không thể check-in lịch này." : "Cần check-in trước khi check-out.");
        return mapper.Map<RecordAttendanceResponse>((await GetAssignmentAsync(request.Id, cancellationToken))!);
    }

    private async Task<EmployeeShift?> GetAssignmentAsync(Guid id, CancellationToken cancellationToken) => await context.EmployeeShifts.AsNoTracking().Include(item => item.Shift).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    private static bool Overlaps(DateOnly firstDate, Shift firstShift, DateOnly secondDate, Shift secondShift) { var first = Interval(firstDate, firstShift); var second = Interval(secondDate, secondShift); return first.Start < second.End && second.Start < first.End; }
    private static (DateTime Start, DateTime End) Interval(DateOnly date, Shift shift)
    {
        var start = date.ToDateTime(shift.StartTime);
        var end = date.ToDateTime(shift.EndTime).AddDays(shift.EndTime < shift.StartTime ? 1 : 0);
        return (TimeZoneInfo.ConvertTimeToUtc(start, VietnamTimeZone), TimeZoneInfo.ConvertTimeToUtc(end, VietnamTimeZone));
    }
}
}

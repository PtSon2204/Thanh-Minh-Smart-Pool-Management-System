using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Contracts;
using SmartPool.Infrastructure.Persistence.TempModels;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed partial class StaffOperations
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    public async Task<OperationResult<PageResult<ScheduleDto>>> GetScheduleAsync(ScheduleListRequest request, CancellationToken cancellationToken)
    {
        if (request.RequireEmployee && (request.EmployeeId is null || !await context.Employees.AsNoTracking().AnyAsync(item => item.UserId == request.EmployeeId, cancellationToken)))
            return OperationResult<PageResult<ScheduleDto>>.Failure(StaffError.Forbidden, "Employee access is required.");
        var query = context.EmployeeShifts.AsNoTracking().Include(item => item.Shift).AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(item => item.EmployeeId == request.EmployeeId);
        if (request.Date is not null) query = query.Where(item => item.WorkDate == request.Date);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(item => item.Status == request.Status);
        var total = await query.CountAsync(cancellationToken); var page = Normalize(request.Page, request.PageSize);
        var rows = await query.OrderBy(item => item.WorkDate).ThenBy(item => item.Shift.StartTime).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return OperationResult<PageResult<ScheduleDto>>.Success(new(rows.Select(ToDto).ToList(), total, page.Page, page.PageSize));
    }

    public async Task<OperationResult<ScheduleDto>> AssignShiftAsync(AssignShiftRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({request.EmployeeId.ToString()}, 0))", cancellationToken);
        var employee = await context.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == request.EmployeeId && item.Status == "Working", cancellationToken);
        if (employee is null) return OperationResult<ScheduleDto>.Failure(StaffError.NotFound, "Working employee was not found.");
        var shift = await context.Shifts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.ShiftId && item.IsActive == true, cancellationToken);
        if (shift is null) return OperationResult<ScheduleDto>.Failure(StaffError.NotFound, "Active shift was not found.");
        var assignments = await context.EmployeeShifts.Include(item => item.Shift).Where(item => item.EmployeeId == request.EmployeeId && item.WorkDate >= request.WorkDate.AddDays(-1) && item.WorkDate <= request.WorkDate.AddDays(1) && item.Status != "Cancelled").ToListAsync(cancellationToken);
        if (assignments.Any(item => Overlaps(request.WorkDate, shift, item.WorkDate, item.Shift))) return OperationResult<ScheduleDto>.Failure(StaffError.Conflict, "Shift overlaps an existing assignment.");
        var assignment = new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = request.EmployeeId, ShiftId = request.ShiftId, WorkDate = request.WorkDate, Status = "Scheduled", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Shift = shift };
        context.EmployeeShifts.Add(assignment); await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return OperationResult<ScheduleDto>.Success(ToDto(assignment));
    }

    public async Task<OperationResult<ScheduleDto>> CancelAssignmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await GetAssignmentAsync(id, cancellationToken);
        if (assignment is null) return OperationResult<ScheduleDto>.Failure(StaffError.NotFound, "Assignment was not found.");
        if (assignment.Status != "Scheduled") return OperationResult<ScheduleDto>.Failure(StaffError.Conflict, "Only scheduled assignments can be cancelled.");
        var now = DateTime.UtcNow;
        var changed = await context.EmployeeShifts.Where(item => item.Id == id && item.Status == "Scheduled").ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Cancelled").SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (changed == 0) return OperationResult<ScheduleDto>.Failure(StaffError.Conflict, "Only scheduled assignments can be cancelled.");
        assignment.Status = "Cancelled";
        assignment.UpdatedAt = now;
        return OperationResult<ScheduleDto>.Success(ToDto(assignment));
    }

    public async Task<OperationResult<ScheduleDto>> RecordAttendanceAsync(Guid id, bool checkIn, Guid? ownerId, CancellationToken cancellationToken)
    {
        var assignment = await GetAssignmentAsync(id, cancellationToken);
        if (assignment is null) return OperationResult<ScheduleDto>.Failure(StaffError.NotFound, "Assignment was not found.");
        if (ownerId is not null && assignment.EmployeeId != ownerId) return OperationResult<ScheduleDto>.Failure(StaffError.Forbidden, "Assignment is not owned by the caller.");
        var now = DateTime.UtcNow;
        var changed = checkIn
            ? await context.EmployeeShifts.Where(item => item.Id == id && item.Status == "Scheduled" && item.CheckInTime == null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Present").SetProperty(item => item.CheckInTime, now).SetProperty(item => item.UpdatedAt, now), cancellationToken)
            : await context.EmployeeShifts.Where(item => item.Id == id && item.Status == "Present" && item.CheckInTime != null && item.CheckOutTime == null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Completed").SetProperty(item => item.CheckOutTime, now).SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (changed == 0) return OperationResult<ScheduleDto>.Failure(StaffError.Conflict, checkIn ? "Check-in is not available." : "Check-out requires one check-in.");
        return OperationResult<ScheduleDto>.Success(ToDto((await GetAssignmentAsync(id, cancellationToken))!));
    }

    private async Task<EmployeeShift?> GetAssignmentAsync(Guid id, CancellationToken cancellationToken) => await context.EmployeeShifts.AsNoTracking().Include(item => item.Shift).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    private static bool Overlaps(DateOnly firstDate, Shift firstShift, DateOnly secondDate, Shift secondShift) { var first = Interval(firstDate, firstShift); var second = Interval(secondDate, secondShift); return first.Start < second.End && second.Start < first.End; }
    private static (DateTime Start, DateTime End) Interval(DateOnly date, Shift shift)
    {
        var start = date.ToDateTime(shift.StartTime);
        var end = date.ToDateTime(shift.EndTime).AddDays(shift.EndTime < shift.StartTime ? 1 : 0);
        return (TimeZoneInfo.ConvertTimeToUtc(start, VietnamTimeZone), TimeZoneInfo.ConvertTimeToUtc(end, VietnamTimeZone));
    }
    private static ScheduleDto ToDto(EmployeeShift assignment) => new(assignment.Id, assignment.EmployeeId, ToDto(assignment.Shift), assignment.WorkDate, assignment.Status ?? string.Empty, assignment.CheckInTime, assignment.CheckOutTime);
}

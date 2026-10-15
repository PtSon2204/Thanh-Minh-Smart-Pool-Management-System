using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch;
using SmartPool.Application.Features.ManageStaffs.Queries.GetScheduleEmployees;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories
{
public sealed partial class StaffOperations
{
    public async Task<IReadOnlyList<GetScheduleEmployeeResponse>> GetScheduleEmployeesAsync(CancellationToken cancellationToken)
    {
        return await EligibleEmployees().AsNoTracking()
            .OrderBy(employee => employee.User.UserProfile!.FullName)
            .ThenBy(employee => employee.UserId)
            .Select(employee => new GetScheduleEmployeeResponse
            {
                EmployeeId = employee.UserId,
                Name = employee.User.UserProfile != null &&
                    employee.User.UserProfile.FullName.Trim() != string.Empty
                    ? employee.User.UserProfile.FullName
                    : employee.User.Username ?? string.Empty,
                EmploymentStatus = employee.Status!
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssignShiftResponse>> AssignShiftBatchAsync(AssignShiftBatchCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockEmployeesAsync(request.Assignments.Select(item => item.EmployeeId), cancellationToken);

        var employeeIds = request.Assignments.Select(item => item.EmployeeId).Distinct().ToArray();
        var shiftIds = request.Assignments.Select(item => item.ShiftId).Distinct().ToArray();
        var employees = await EligibleEmployees().AsNoTracking()
            .Where(item => employeeIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, cancellationToken);
        var shifts = await context.Shifts.AsNoTracking()
            .Where(item => shiftIds.Contains(item.Id) && item.IsActive == true)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var minimumDate = request.Assignments.Min(item => item.WorkDate).AddDays(-1);
        var maximumDate = request.Assignments.Max(item => item.WorkDate).AddDays(1);
        var storedAssignments = await context.EmployeeShifts.AsNoTracking().Include(item => item.Shift)
            .Where(item => employeeIds.Contains(item.EmployeeId) && item.WorkDate >= minimumDate && item.WorkDate <= maximumDate)
            .ToListAsync(cancellationToken);

        var conflicts = FindBatchConflicts(request.Assignments, employees, shifts, storedAssignments);
        if (conflicts.Count != 0) throw new AssignShiftBatchConflictException(conflicts);

        var now = DateTime.UtcNow;
        var assignments = request.Assignments.Select(item => new EmployeeShift
        {
            Id = Guid.NewGuid(), EmployeeId = item.EmployeeId, ShiftId = item.ShiftId,
            WorkDate = item.WorkDate, Status = "Scheduled", CreatedAt = now, UpdatedAt = now
        }).ToList();
        context.EmployeeShifts.AddRange(assignments);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsExactTupleViolation(exception))
        {
            throw new AssignShiftBatchConflictException(request.Assignments
                .Select(item => Conflict(item, "exact_tuple", "Phân công này đã tồn tại."))
                .ToList());
        }
        await transaction.CommitAsync(cancellationToken);
        return assignments.Select(assignment =>
        {
            var shift = shifts[assignment.ShiftId];
            return new AssignShiftResponse
            {
                Id = assignment.Id,
                EmployeeId = assignment.EmployeeId,
                ShiftId = assignment.ShiftId,
                ShiftName = shift.Name,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                IsShiftActive = shift.IsActive == true,
                WorkDate = assignment.WorkDate,
                Status = assignment.Status ?? string.Empty
            };
        }).ToList();
    }

    private static List<AssignShiftConflict> FindBatchConflicts(
        IReadOnlyList<AssignShiftBatchItem> requests,
        IReadOnlyDictionary<Guid, Employee> employees,
        IReadOnlyDictionary<Guid, Shift> shifts,
        IReadOnlyCollection<EmployeeShift> storedAssignments)
    {
        var conflicts = new List<AssignShiftConflict>();
        var accepted = new List<AssignShiftBatchItem>();
        foreach (var request in requests)
        {
            AssignShiftConflict? conflict = null;
            if (!employees.ContainsKey(request.EmployeeId))
                conflict = Conflict(request, "employee_ineligible", "Không tìm thấy nhân viên đang làm việc.");
            else if (!shifts.TryGetValue(request.ShiftId, out var shift))
                conflict = Conflict(request, "shift_unavailable", "Không tìm thấy ca làm việc đang hoạt động.");
            else if (storedAssignments.Any(item => item.EmployeeId == request.EmployeeId && item.ShiftId == request.ShiftId && item.WorkDate == request.WorkDate) ||
                accepted.Any(item => item.EmployeeId == request.EmployeeId && item.ShiftId == request.ShiftId && item.WorkDate == request.WorkDate))
                conflict = Conflict(request, "exact_tuple", "Phân công này đã tồn tại.");
            else if (storedAssignments.Any(item => item.EmployeeId == request.EmployeeId && item.Status != "Cancelled" && Overlaps(request.WorkDate, shift, item.WorkDate, item.Shift)) ||
                accepted.Any(item => item.EmployeeId == request.EmployeeId && Overlaps(request.WorkDate, shift, item.WorkDate, shifts[item.ShiftId])))
                conflict = Conflict(request, "overlap", "Ca làm việc bị trùng với lịch đã phân công.");

            if (conflict is null) accepted.Add(request);
            else conflicts.Add(conflict);
        }
        return conflicts;
    }

    private static AssignShiftConflict Conflict(AssignShiftBatchItem item, string code, string message) =>
        new(item.EmployeeId, item.ShiftId, item.WorkDate, code, message);
}
}

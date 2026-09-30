namespace SmartPool.Application.Features.ManageStaffs.Contracts;

public sealed record ShiftDto(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime, bool IsActive);
public sealed record SaveShiftRequest(string Name, TimeOnly StartTime, TimeOnly EndTime, bool IsActive);
public sealed record ShiftListRequest(int Page = 1, int PageSize = 20, bool? Active = null);
public sealed record ScheduleDto(Guid Id, Guid EmployeeId, ShiftDto Shift, DateOnly WorkDate, string Status,
    DateTime? CheckInTime, DateTime? CheckOutTime);
public sealed record ScheduleListRequest(int Page = 1, int PageSize = 20, Guid? EmployeeId = null,
    DateOnly? Date = null, string? Status = null, bool RequireEmployee = false);
public sealed record AssignShiftRequest(Guid EmployeeId, Guid ShiftId, DateOnly WorkDate);

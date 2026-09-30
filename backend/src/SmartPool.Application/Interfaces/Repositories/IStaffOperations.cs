using SmartPool.Application.Features.ManageStaffs.Contracts;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IStaffOperations
{
    Task<OperationResult<PageResult<StaffDto>>> GetStaffsAsync(StaffListRequest request, CancellationToken cancellationToken);
    Task<OperationResult<StaffOptionsDto>> GetOptionsAsync(CancellationToken cancellationToken);
    Task<OperationResult<StaffDto>> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken);
    Task<OperationResult<StaffDto>> UpdateStaffAsync(Guid userId, UpdateStaffRequest request, CancellationToken cancellationToken);
    Task<OperationResult<PageResult<ShiftDto>>> GetShiftsAsync(ShiftListRequest request, CancellationToken cancellationToken);
    Task<OperationResult<ShiftDto>> SaveShiftAsync(Guid? id, SaveShiftRequest request, CancellationToken cancellationToken);
    Task<OperationResult<PageResult<ScheduleDto>>> GetScheduleAsync(ScheduleListRequest request, CancellationToken cancellationToken);
    Task<OperationResult<ScheduleDto>> AssignShiftAsync(AssignShiftRequest request, CancellationToken cancellationToken);
    Task<OperationResult<ScheduleDto>> CancelAssignmentAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResult<ScheduleDto>> RecordAttendanceAsync(Guid id, bool checkIn, Guid? ownerId, CancellationToken cancellationToken);
    Task<OperationResult<PageResult<SalaryDto>>> GetSalariesAsync(SalaryListRequest request, CancellationToken cancellationToken);
    Task<OperationResult<SalaryDto>> SaveSalaryAsync(Guid userId, Guid? id, SaveSalaryRequest request, CancellationToken cancellationToken);
}

using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch;
using SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Features.ManageStaffs.Commands.SaveShift;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Queries.GetAttendanceHistory;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;
using SmartPool.Application.Features.ManageStaffs.Queries.GetScheduleEmployees;
using SmartPool.Application.Features.ManageStaffs.Queries.GetShifts;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffOptions;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffs;
using SmartPool.Application.Features.Notifications.Queries.GetNotifications;

namespace SmartPool.Application.Interfaces.Repositories
{

public interface IStaffOperations
{
    Task<GetNotificationsResult> GetMyNotificationsAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<bool> MarkMyNotificationReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);
    Task<object> ExecuteWorkPatternAsync(SmartPool.Application.Features.ManageStaffs.WorkPatterns.WorkPatternCommand request, CancellationToken cancellationToken);
    Task<SmartPool.Application.Features.ManageStaffs.WorkPatterns.StaffShiftReplacementResult> ReplaceStaffShiftAsync(
        SmartPool.Application.Features.ManageStaffs.WorkPatterns.ReplaceStaffShiftCommand request, CancellationToken cancellationToken);
    Task<OnboardingWorkspace> ExecuteOnboardingAsync(OnboardingCommand request, CancellationToken cancellationToken);
    Task<IReadOnlyList<StaffInvitationListItem>> ListInvitationsAsync(ListStaffInvitationsQuery request, CancellationToken cancellationToken);
    Task<StaffDocumentContent> ReadStaffDocumentAsync(ReadStaffDocumentQuery request, CancellationToken cancellationToken);
    Task<PagedResponse<GetStaffsResponse>> GetStaffsAsync(GetStaffsQuery request, CancellationToken cancellationToken);
    Task<GetStaffOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken);
    Task<CreateStaffResponse> CreateStaffAsync(CreateStaffCommand request, CancellationToken cancellationToken);
    Task<UpdateStaffResponse> UpdateStaffAsync(UpdateStaffCommand request, CancellationToken cancellationToken);
    Task<PagedResponse<GetShiftsResponse>> GetShiftsAsync(GetShiftsQuery request, CancellationToken cancellationToken);
    Task<SaveShiftResponse> SaveShiftAsync(SaveShiftCommand request, CancellationToken cancellationToken);
    Task<PagedResponse<GetScheduleResponse>> GetScheduleAsync(GetScheduleQuery request, CancellationToken cancellationToken);
    Task<IReadOnlyList<GetScheduleEmployeeResponse>> GetScheduleEmployeesAsync(CancellationToken cancellationToken);
    Task<AssignShiftResponse> AssignShiftAsync(AssignShiftCommand request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssignShiftResponse>> AssignShiftBatchAsync(AssignShiftBatchCommand request, CancellationToken cancellationToken);
    Task<CancelShiftAssignmentResponse> CancelAssignmentAsync(CancelShiftAssignmentCommand request, CancellationToken cancellationToken);
    Task<RecordAttendanceResponse> RecordAttendanceAsync(RecordAttendanceCommand request, CancellationToken cancellationToken);
    Task<RecordAttendanceResponse> CorrectAttendanceAsync(ManualAttendanceCorrectionCommand request, CancellationToken cancellationToken);
    Task<PagedResponse<GetAttendanceHistoryResponse>> GetAttendanceHistoryAsync(GetAttendanceHistoryQuery query, CancellationToken cancellationToken);
}
}

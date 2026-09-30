using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageStaffs.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands;

public sealed record AssignShiftCommand(AssignShiftRequest Request) : IRequest<OperationResult<ScheduleDto>>;
public sealed class AssignShiftHandler(IStaffOperations operations) : IRequestHandler<AssignShiftCommand, OperationResult<ScheduleDto>>
{ public async Task<OperationResult<ScheduleDto>> Handle(AssignShiftCommand command, CancellationToken cancellationToken) { var validation = new AssignShiftValidator().Validate(command.Request); return !validation.IsValid ? OperationResult<ScheduleDto>.Failure(StaffError.Validation, validation.Errors[0].ErrorMessage) : await operations.AssignShiftAsync(command.Request, cancellationToken); } }
public sealed class AssignShiftValidator : AbstractValidator<AssignShiftRequest>
{ public AssignShiftValidator() { RuleFor(x => x.EmployeeId).NotEmpty(); RuleFor(x => x.ShiftId).NotEmpty(); } }

public sealed record CancelShiftAssignmentCommand(Guid Id) : IRequest<OperationResult<ScheduleDto>>;
public sealed class CancelShiftAssignmentHandler(IStaffOperations operations) : IRequestHandler<CancelShiftAssignmentCommand, OperationResult<ScheduleDto>>
{ public Task<OperationResult<ScheduleDto>> Handle(CancelShiftAssignmentCommand command, CancellationToken cancellationToken) => operations.CancelAssignmentAsync(command.Id, cancellationToken); }

public sealed record RecordAttendanceCommand(Guid Id, bool CheckIn, Guid? OwnerId) : IRequest<OperationResult<ScheduleDto>>;
public sealed class RecordAttendanceHandler(IStaffOperations operations) : IRequestHandler<RecordAttendanceCommand, OperationResult<ScheduleDto>>
{ public Task<OperationResult<ScheduleDto>> Handle(RecordAttendanceCommand command, CancellationToken cancellationToken) => operations.RecordAttendanceAsync(command.Id, command.CheckIn, command.OwnerId, cancellationToken); }

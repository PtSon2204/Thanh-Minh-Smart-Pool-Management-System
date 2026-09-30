using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageStaffs.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands;

public sealed record CreateStaffCommand(CreateStaffRequest Request) : IRequest<OperationResult<StaffDto>>;
public sealed class CreateStaffHandler(IStaffOperations operations) : IRequestHandler<CreateStaffCommand, OperationResult<StaffDto>>
{ public async Task<OperationResult<StaffDto>> Handle(CreateStaffCommand command, CancellationToken cancellationToken) { var validation = new CreateStaffValidator().Validate(command.Request); return !validation.IsValid ? OperationResult<StaffDto>.Failure(StaffError.Validation, validation.Errors[0].ErrorMessage) : await operations.CreateStaffAsync(command.Request, cancellationToken); } }
public sealed class CreateStaffValidator : AbstractValidator<CreateStaffRequest>
{ public CreateStaffValidator() { RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.RoleId).NotEmpty(); RuleFor(x => x.FullName).NotEmpty().MaximumLength(255); RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0); RuleFor(x => x.Status).Must(x => x is "Working" or "Inactive"); } }

public sealed record UpdateStaffCommand(Guid UserId, UpdateStaffRequest Request) : IRequest<OperationResult<StaffDto>>;
public sealed class UpdateStaffHandler(IStaffOperations operations) : IRequestHandler<UpdateStaffCommand, OperationResult<StaffDto>>
{ public async Task<OperationResult<StaffDto>> Handle(UpdateStaffCommand command, CancellationToken cancellationToken) { var validation = new UpdateStaffValidator().Validate(command.Request); return !validation.IsValid ? OperationResult<StaffDto>.Failure(StaffError.Validation, validation.Errors[0].ErrorMessage) : await operations.UpdateStaffAsync(command.UserId, command.Request, cancellationToken); } }
public sealed class UpdateStaffValidator : AbstractValidator<UpdateStaffRequest>
{ public UpdateStaffValidator() { RuleFor(x => x.RoleId).NotEmpty(); RuleFor(x => x.FullName).NotEmpty().MaximumLength(255); RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0); RuleFor(x => x.Status).Must(x => x is "Working" or "Inactive"); } }

public sealed record SaveShiftCommand(Guid? Id, SaveShiftRequest Request) : IRequest<OperationResult<ShiftDto>>;
public sealed class SaveShiftHandler(IStaffOperations operations) : IRequestHandler<SaveShiftCommand, OperationResult<ShiftDto>>
{ public async Task<OperationResult<ShiftDto>> Handle(SaveShiftCommand command, CancellationToken cancellationToken) { var validation = new SaveShiftValidator().Validate(command.Request); return !validation.IsValid ? OperationResult<ShiftDto>.Failure(StaffError.Validation, validation.Errors[0].ErrorMessage) : await operations.SaveShiftAsync(command.Id, command.Request, cancellationToken); } }
public sealed class SaveShiftValidator : AbstractValidator<SaveShiftRequest>
{ public SaveShiftValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(100); RuleFor(x => x).Must(x => x.StartTime != x.EndTime).WithMessage("Shift start and end times must differ."); } }

public sealed record SaveSalaryCommand(Guid UserId, Guid? Id, SaveSalaryRequest Request) : IRequest<OperationResult<SalaryDto>>;
public sealed class SaveSalaryHandler(IStaffOperations operations) : IRequestHandler<SaveSalaryCommand, OperationResult<SalaryDto>>
{ public async Task<OperationResult<SalaryDto>> Handle(SaveSalaryCommand command, CancellationToken cancellationToken) { var validation = new SaveSalaryValidator().Validate(command.Request); return !validation.IsValid ? OperationResult<SalaryDto>.Failure(StaffError.Validation, validation.Errors[0].ErrorMessage) : await operations.SaveSalaryAsync(command.UserId, command.Id, command.Request, cancellationToken); } }
public sealed class SaveSalaryValidator : AbstractValidator<SaveSalaryRequest>
{ public SaveSalaryValidator() { RuleFor(x => x.Month).InclusiveBetween(1, 12); RuleFor(x => x.Year).InclusiveBetween(1, 9999); RuleFor(x => x.TotalShifts).GreaterThanOrEqualTo(0); RuleFor(x => x.Bonus).GreaterThanOrEqualTo(0); RuleFor(x => x.Deduction).GreaterThanOrEqualTo(0); RuleFor(x => x.NetSalary).GreaterThanOrEqualTo(0); } }

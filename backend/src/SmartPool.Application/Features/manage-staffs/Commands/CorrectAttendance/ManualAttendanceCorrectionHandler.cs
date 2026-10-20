using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;

public sealed class ManualAttendanceCorrectionHandler : IRequestHandler<ManualAttendanceCorrectionCommand, RecordAttendanceResponse>
{
    private readonly IStaffOperations operations;
    private readonly IValidator<ManualAttendanceCorrectionCommand> validator;

    public ManualAttendanceCorrectionHandler(IStaffOperations operations, IValidator<ManualAttendanceCorrectionCommand> validator)
    {
        this.operations = operations;
        this.validator = validator;
    }

    public async Task<RecordAttendanceResponse> Handle(ManualAttendanceCorrectionCommand request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.CorrectAttendanceAsync(request, cancellationToken);
    }
}

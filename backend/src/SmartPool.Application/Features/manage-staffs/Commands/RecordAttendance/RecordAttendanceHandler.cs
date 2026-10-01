using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance
{
    public sealed class RecordAttendanceHandler : IRequestHandler<RecordAttendanceCommand, RecordAttendanceResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<RecordAttendanceCommand> validator;

        public RecordAttendanceHandler(IStaffOperations operations, IValidator<RecordAttendanceCommand> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<RecordAttendanceResponse> Handle(RecordAttendanceCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.RecordAttendanceAsync(request, cancellationToken);
        }
    }
}

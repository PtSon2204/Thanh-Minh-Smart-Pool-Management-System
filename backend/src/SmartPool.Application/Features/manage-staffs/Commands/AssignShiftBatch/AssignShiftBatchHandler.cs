using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch
{
    public sealed class AssignShiftBatchHandler : IRequestHandler<AssignShiftBatchCommand, IReadOnlyList<AssignShiftResponse>>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<AssignShiftBatchCommand> validator;

        public AssignShiftBatchHandler(IStaffOperations operations, IValidator<AssignShiftBatchCommand> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<IReadOnlyList<AssignShiftResponse>> Handle(AssignShiftBatchCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.AssignShiftBatchAsync(request, cancellationToken);
        }
    }
}

using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment
{
    public sealed class CancelShiftAssignmentHandler : IRequestHandler<CancelShiftAssignmentCommand, CancelShiftAssignmentResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<CancelShiftAssignmentCommand> validator;

        public CancelShiftAssignmentHandler(IStaffOperations operations, IValidator<CancelShiftAssignmentCommand> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<CancelShiftAssignmentResponse> Handle(CancelShiftAssignmentCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.CancelAssignmentAsync(request, cancellationToken);
        }
    }
}

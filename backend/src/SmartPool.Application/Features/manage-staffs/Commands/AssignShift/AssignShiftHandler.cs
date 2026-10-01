using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShift
{
    public sealed class AssignShiftHandler : IRequestHandler<AssignShiftCommand, AssignShiftResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<AssignShiftCommand> validator;

        public AssignShiftHandler(IStaffOperations operations, IValidator<AssignShiftCommand> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<AssignShiftResponse> Handle(AssignShiftCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.AssignShiftAsync(request, cancellationToken);
        }
    }
}

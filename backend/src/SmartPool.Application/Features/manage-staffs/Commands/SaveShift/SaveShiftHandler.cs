using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.SaveShift
{
    public sealed class SaveShiftHandler : IRequestHandler<SaveShiftCommand, SaveShiftResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<SaveShiftCommand> validator;

        public SaveShiftHandler(IStaffOperations operations, IValidator<SaveShiftCommand> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<SaveShiftResponse> Handle(SaveShiftCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.SaveShiftAsync(request, cancellationToken);
        }
    }
}

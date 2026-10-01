using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff
{
    public sealed class UpdateStaffHandler : IRequestHandler<UpdateStaffCommand, UpdateStaffResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<UpdateStaffCommand> validator;

        public UpdateStaffHandler(IStaffOperations operations, IValidator<UpdateStaffCommand> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<UpdateStaffResponse> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.UpdateStaffAsync(request, cancellationToken);
        }
    }
}

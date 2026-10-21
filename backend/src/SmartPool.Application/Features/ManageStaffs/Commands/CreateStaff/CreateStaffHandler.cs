using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff
{
    public sealed class CreateStaffHandler : IRequestHandler<CreateStaffCommand, CreateStaffResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<CreateStaffCommand> validator;

        public CreateStaffHandler(IStaffOperations operations, IValidator<CreateStaffCommand> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<CreateStaffResponse> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.CreateStaffAsync(request, cancellationToken);
        }
    }
}

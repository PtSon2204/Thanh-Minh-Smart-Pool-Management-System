using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.SaveSalary
{
    public sealed class SaveSalaryHandler : IRequestHandler<SaveSalaryCommand, SaveSalaryResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<SaveSalaryCommand> validator;

        public SaveSalaryHandler(IStaffOperations operations, IValidator<SaveSalaryCommand> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<SaveSalaryResponse> Handle(SaveSalaryCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.SaveSalaryAsync(request, cancellationToken);
        }
    }
}

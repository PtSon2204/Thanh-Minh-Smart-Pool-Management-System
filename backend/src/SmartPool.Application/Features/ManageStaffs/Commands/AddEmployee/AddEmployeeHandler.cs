using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;

public sealed class AddEmployeeHandler(IStaffOperations operations, IValidator<AddEmployeeCommand> validator)
    : IRequestHandler<AddEmployeeCommand, AddEmployeeResponse>
{
    public async Task<AddEmployeeResponse> Handle(AddEmployeeCommand request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.AddEmployeeAsync(request, cancellationToken);
    }
}

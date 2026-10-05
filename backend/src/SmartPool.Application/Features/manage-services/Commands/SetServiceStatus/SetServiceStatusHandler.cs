using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands.SetServiceStatus;

public sealed class SetServiceStatusHandler : IRequestHandler<SetServiceStatusCommand, SetServiceStatusResponse>
{
    private readonly IServiceOperations _operations;
    private readonly IValidator<SetServiceStatusCommand> _validator;

    public SetServiceStatusHandler(IServiceOperations operations, IValidator<SetServiceStatusCommand> validator)
    {
        _operations = operations;
        _validator = validator;
    }

    public async Task<SetServiceStatusResponse> Handle(SetServiceStatusCommand command, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);
        return await _operations.SetServiceStatusAsync(command, cancellationToken);
    }
}

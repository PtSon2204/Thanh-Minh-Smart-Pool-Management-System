using FluentValidation;
using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.AccessControlPool.Commands;

public sealed record ConfirmEntryCommand(string? Code, string? InputMode, Guid OperatorId)
    : IRequest<ValidationResult<EntryConfirmationResult>>;

public sealed class ConfirmEntryValidator : AbstractValidator<ConfirmEntryCommand>
{
    public ConfirmEntryValidator()
    {
        RuleFor(command => command.Code).NotEmpty().Must(code => !string.IsNullOrWhiteSpace(code)).MaximumLength(255);
        RuleFor(command => command.InputMode).Must(mode =>
            mode is PoolAccessValues.Manual or PoolAccessValues.Qr);
    }
}

public sealed class ConfirmEntryHandler(
    IPoolAccessOperations operations,
    IValidator<ConfirmEntryCommand> validator)
    : IRequestHandler<ConfirmEntryCommand, ValidationResult<EntryConfirmationResult>>
{
    public async Task<ValidationResult<EntryConfirmationResult>> Handle(
        ConfirmEntryCommand request,
        CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim();
        var validation = await validator.ValidateAsync(request with { Code = code }, cancellationToken);
        if (!validation.IsValid)
        {
            var failure = validation.Errors[0];
            return ValidationResult<EntryConfirmationResult>.Failure(failure.PropertyName, failure.ErrorMessage);
        }

        var result = await operations.ConfirmAsync(
            code!, request.InputMode!, request.OperatorId, cancellationToken);
        return ValidationResult<EntryConfirmationResult>.Success(result);
    }
}

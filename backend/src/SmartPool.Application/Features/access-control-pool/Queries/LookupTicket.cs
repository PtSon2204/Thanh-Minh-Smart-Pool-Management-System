using FluentValidation;
using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.AccessControlPool.Queries;

public sealed record LookupTicketQuery(string? Code) : IRequest<ValidationResult<TicketLookupResult>>;

public sealed class LookupTicketValidator : AbstractValidator<LookupTicketQuery>
{
    public LookupTicketValidator() => RuleFor(query => query.Code).NotEmpty().Must(code => !string.IsNullOrWhiteSpace(code)).MaximumLength(255);
}

public sealed class LookupTicketHandler(
    IPoolAccessOperations operations,
    IValidator<LookupTicketQuery> validator)
    : IRequestHandler<LookupTicketQuery, ValidationResult<TicketLookupResult>>
{
    public async Task<ValidationResult<TicketLookupResult>> Handle(
        LookupTicketQuery request,
        CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim();
        var validation = await validator.ValidateAsync(request with { Code = code }, cancellationToken);
        if (!validation.IsValid)
        {
            var failure = validation.Errors[0];
            return ValidationResult<TicketLookupResult>.Failure(failure.PropertyName, failure.ErrorMessage);
        }

        var result = await operations.LookupAsync(code!, DateTime.UtcNow, cancellationToken);
        return ValidationResult<TicketLookupResult>.Success(result);
    }
}

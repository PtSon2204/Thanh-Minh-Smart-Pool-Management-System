using FluentValidation;
using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.AccessControlPool.Queries;

public sealed record GetDailyEntrySummaryQuery(DateOnly Date) : IRequest<ValidationResult<DailyEntrySummaryDto>>;

public sealed class GetDailyEntrySummaryValidator : AbstractValidator<GetDailyEntrySummaryQuery>
{
    public GetDailyEntrySummaryValidator() => RuleFor(query => query.Date)
        .NotEmpty()
        .LessThan(DateOnly.MaxValue)
        .WithMessage("Date must allow a following local day.");
}

public sealed class GetDailyEntrySummaryHandler(
    IPoolAccessOperations operations,
    IValidator<GetDailyEntrySummaryQuery> validator)
    : IRequestHandler<GetDailyEntrySummaryQuery, ValidationResult<DailyEntrySummaryDto>>
{
    public async Task<ValidationResult<DailyEntrySummaryDto>> Handle(
        GetDailyEntrySummaryQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var failure = validation.Errors[0];
            return ValidationResult<DailyEntrySummaryDto>.Failure(failure.PropertyName, failure.ErrorMessage);
        }

        var result = await operations.GetDailySummaryAsync(request.Date, cancellationToken);
        return ValidationResult<DailyEntrySummaryDto>.Success(result);
    }
}

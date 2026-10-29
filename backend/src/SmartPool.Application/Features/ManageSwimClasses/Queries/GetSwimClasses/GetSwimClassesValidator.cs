using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;

public sealed class GetSwimClassesValidator : AbstractValidator<GetSwimClassesQuery>
{
    public GetSwimClassesValidator()
    {
        RuleFor(query => query.PageIndex).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.SearchTerm).MaximumLength(150);
        RuleFor(query => query.Status).MaximumLength(30);
        RuleFor(query => query.Scope)
            .Must(scope => string.IsNullOrWhiteSpace(scope) ||
                new[] { "customer", "coach" }.Contains(scope.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Phạm vi xem lớp không hợp lệ.");
    }
}

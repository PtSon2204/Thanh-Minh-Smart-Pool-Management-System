using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMySwimClasses;

public sealed class GetMySwimClassesValidator : AbstractValidator<GetMySwimClassesQuery>
{
    private static readonly string[] AllowedStatuses = ["Confirmed", "Cancelled"];

    public GetMySwimClassesValidator()
    {
        RuleFor(query => query.PageIndex).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => string.IsNullOrWhiteSpace(status) ||
                AllowedStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Trạng thái đăng ký không hợp lệ.");
    }
}

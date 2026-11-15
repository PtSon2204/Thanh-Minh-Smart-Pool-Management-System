using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassAttendances;

public sealed class GetMyClassAttendancesValidator : AbstractValidator<GetMyClassAttendancesQuery>
{
    public GetMyClassAttendancesValidator()
    {
        RuleFor(query => query.ClassId).NotEmpty();
        RuleFor(query => query.PageIndex).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetAttendanceHistory;

public sealed class GetAttendanceHistoryValidator : AbstractValidator<GetAttendanceHistoryQuery>
{
    public GetAttendanceHistoryValidator()
    {
        RuleFor(query => query.AssignmentId).NotEmpty();
        RuleFor(query => query.PageIndex).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

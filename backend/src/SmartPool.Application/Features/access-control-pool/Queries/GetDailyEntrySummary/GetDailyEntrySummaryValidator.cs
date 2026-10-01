using FluentValidation;

namespace SmartPool.Application.Features.AccessControlPool.Queries.GetDailyEntrySummary
{
    public class GetDailyEntrySummaryValidator : AbstractValidator<GetDailyEntrySummaryQuery>
    {
        public GetDailyEntrySummaryValidator()
        {
            RuleFor(query => query.Date)
                .NotEmpty().WithMessage("Ngày không được để trống.")
                .LessThan(DateOnly.MaxValue).WithMessage("Ngày phải cho phép xác định ngày kế tiếp.");
        }
    }
}

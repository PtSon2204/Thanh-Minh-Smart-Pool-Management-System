using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule
{
    public sealed class GetScheduleValidator : AbstractValidator<GetScheduleQuery>
    {
        public GetScheduleValidator()
        {
            RuleFor(item => item.PageIndex).GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
            RuleFor(item => item.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
        }
    }
}

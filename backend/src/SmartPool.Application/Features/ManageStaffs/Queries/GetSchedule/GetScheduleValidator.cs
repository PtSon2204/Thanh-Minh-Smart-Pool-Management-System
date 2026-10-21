using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule
{
    public sealed class GetScheduleValidator : AbstractValidator<GetScheduleQuery>
    {
        public GetScheduleValidator()
        {
            RuleFor(item => item.PageIndex).GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
            RuleFor(item => item.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
            RuleFor(item => item)
                .Must(item => item.FromDate.HasValue == item.ToDate.HasValue)
                .WithMessage("fromDate và toDate phải được cung cấp cùng nhau.");
            RuleFor(item => item)
                .Must(item => item.Date is null || (!item.FromDate.HasValue && !item.ToDate.HasValue))
                .WithMessage("Không thể dùng date cùng với fromDate và toDate.");
            RuleFor(item => item)
                .Must(item => !item.FromDate.HasValue || !item.ToDate.HasValue || item.FromDate <= item.ToDate)
                .WithMessage("fromDate phải nhỏ hơn hoặc bằng toDate.");
            RuleFor(item => item)
                .Must(item => !item.FromDate.HasValue || !item.ToDate.HasValue || item.ToDate.Value.DayNumber - item.FromDate.Value.DayNumber <= 8)
                .WithMessage("Khoảng ngày không được quá 9 ngày.");
        }
    }
}

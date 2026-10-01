using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetSalaries
{
    public sealed class GetSalariesValidator : AbstractValidator<GetSalariesQuery>
    {
        public GetSalariesValidator()
        {
            RuleFor(item => item.PageIndex).GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
            RuleFor(item => item.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
            RuleFor(item => item.Month).InclusiveBetween(1, 12).When(item => item.Month.HasValue).WithMessage("Tháng phải từ 1 đến 12.");
            RuleFor(item => item.Year).InclusiveBetween(1, 9999).When(item => item.Year.HasValue).WithMessage("Năm không hợp lệ.");
        }
    }
}

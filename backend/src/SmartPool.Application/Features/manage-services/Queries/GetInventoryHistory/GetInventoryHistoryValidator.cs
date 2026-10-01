using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory
{
    public sealed class GetInventoryHistoryValidator : AbstractValidator<GetInventoryHistoryQuery>
    {
        public GetInventoryHistoryValidator()
        {
            RuleFor(query => query.ServiceId).NotEmpty().WithMessage("Mã dịch vụ không hợp lệ.");
            RuleFor(query => query.PageIndex).GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
        }
    }
}

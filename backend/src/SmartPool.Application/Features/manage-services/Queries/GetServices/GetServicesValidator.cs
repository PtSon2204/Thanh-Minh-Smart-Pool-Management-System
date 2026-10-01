using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Queries.GetServices
{
    public sealed class GetServicesValidator : AbstractValidator<GetServicesQuery>
    {
        public GetServicesValidator()
        {
            RuleFor(query => query.PageIndex).GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
            RuleFor(query => query.Type).Must(type => string.IsNullOrWhiteSpace(type) || type is "Sale" or "Rental")
                .WithMessage("Loại dịch vụ phải là Sale hoặc Rental.");
        }
    }
}

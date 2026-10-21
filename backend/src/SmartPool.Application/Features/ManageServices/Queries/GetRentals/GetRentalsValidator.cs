using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Queries.GetRentals
{
    public sealed class GetRentalsValidator : AbstractValidator<GetRentalsQuery>
    {
        public GetRentalsValidator()
        {
            RuleFor(query => query.PageIndex).GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
            RuleFor(query => query.Status).Must(status => string.IsNullOrWhiteSpace(status) || status is "Renting" or "Returned")
                .WithMessage("Trạng thái thuê phải là Renting hoặc Returned.");
        }
    }
}

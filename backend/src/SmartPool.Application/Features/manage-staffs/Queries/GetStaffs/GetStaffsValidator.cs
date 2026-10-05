using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffs
{
    public sealed class GetStaffsValidator : AbstractValidator<GetStaffsQuery>
    {
        public GetStaffsValidator()
        {
            RuleFor(item => item.PageIndex).GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
            RuleFor(item => item.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
            RuleFor(item => item.RoleName)
                .Must(role => string.IsNullOrWhiteSpace(role) || string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "STAFF", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Vai trò phải là ADMIN hoặc STAFF.");
        }
    }
}

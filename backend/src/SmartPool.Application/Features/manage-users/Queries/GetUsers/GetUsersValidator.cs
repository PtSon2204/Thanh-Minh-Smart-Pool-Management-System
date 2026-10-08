using FluentValidation;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageUsers.Queries.GetUsers;

public sealed class GetUsersValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersValidator()
    {
        RuleFor(query => query.PageIndex)
            .GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
        RuleFor(query => query.SearchTerm)
            .Must(value => value is null || value.Trim().Length <= 100)
            .WithMessage("Từ khóa tìm kiếm không được vượt quá 100 ký tự.");
        RuleFor(query => query.Role)
            .Must(value => string.IsNullOrWhiteSpace(value) || Enum.GetNames<RoleEnum>().Any(name =>
                string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Vai trò phải là CUSTOMER, STAFF hoặc ADMIN.");
        RuleFor(query => query.Status)
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                string.Equals(value.Trim(), nameof(UserStatusEnum.ACTIVE), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value.Trim(), nameof(UserStatusEnum.LOCKED), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Trạng thái phải là ACTIVE hoặc LOCKED.");
    }
}

using FluentValidation;
using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.Application.Features.AccessControlPool.Queries.GetEntryHistory
{
    public class GetEntryHistoryValidator : AbstractValidator<GetEntryHistoryQuery>
    {
        public GetEntryHistoryValidator()
        {
            RuleFor(query => query.PageIndex).GreaterThan(0).WithMessage("Trang phải lớn hơn 0.");
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
            RuleFor(query => query).Must(query => ((long)query.PageIndex - 1) * query.PageSize <= int.MaxValue)
                .WithName(nameof(GetEntryHistoryQuery.PageIndex))
                .WithMessage("Chỉ số trang không hợp lệ.");
            RuleFor(query => query.Date).Must(date => date is null || date.Value < DateOnly.MaxValue)
                .WithMessage("Ngày phải cho phép xác định ngày kế tiếp.");
            RuleFor(query => query.Status).Must(status => status is null or PoolAccessValues.Allowed or PoolAccessValues.Denied)
                .WithMessage("Trạng thái lượt vào không hợp lệ.");
        }
    }
}

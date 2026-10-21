using FluentValidation;
using SmartPool.Application.Common.Helpers;

namespace SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers
{
    public sealed class GetAllVouchersValidator : AbstractValidator<GetAllVouchersQuery>
    {
        public GetAllVouchersValidator()
        {
            RuleFor(item => item.PageIndex).GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
            RuleFor(item => item.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
            RuleFor(item => item.Status)
                .Must(status => status is null or VoucherValidityPolicy.Active or VoucherValidityPolicy.Expired
                    or VoucherValidityPolicy.Scheduled or VoucherValidityPolicy.Locked)
                .WithMessage("Trạng thái voucher không hợp lệ.");
        }
    }
}

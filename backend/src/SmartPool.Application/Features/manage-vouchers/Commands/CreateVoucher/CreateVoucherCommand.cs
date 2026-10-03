using MediatR;

namespace SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher
{
    public sealed class CreateVoucherCommand : IRequest<CreateVoucherResponse>
    {
        public string Code { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal? MinOrderValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool? IsActive { get; set; }
    }
}

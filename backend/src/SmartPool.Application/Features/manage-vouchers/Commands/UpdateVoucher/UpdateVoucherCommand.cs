using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageVouchers.Commands.UpdateVoucher
{
    public sealed class UpdateVoucherCommand : IRequest<UpdateVoucherResponse>
    {
        [JsonIgnore]
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal? MinOrderValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool? IsActive { get; set; }
    }
}

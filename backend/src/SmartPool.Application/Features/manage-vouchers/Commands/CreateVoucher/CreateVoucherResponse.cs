namespace SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher
{
    public sealed class CreateVoucherResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}

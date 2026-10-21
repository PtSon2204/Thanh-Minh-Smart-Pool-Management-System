namespace SmartPool.Application.Features.ManageVouchers
{
    public sealed class VoucherConflictException : Exception
    {
        public VoucherConflictException(string message) : base(message)
        {
        }
    }
}

using MediatR;

namespace SmartPool.Application.Features.ManageVouchers.Commands.ToggleLockVoucher
{
    public sealed class ToggleLockVoucherCommand : IRequest<bool>
    {
        public Guid Id { get; set; }
    }
}

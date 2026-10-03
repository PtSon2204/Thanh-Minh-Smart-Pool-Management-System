using MediatR;

namespace SmartPool.Application.Features.ManageVouchers.Commands.DeleteVoucher
{
    public sealed class DeleteVoucherCommand : IRequest<bool>
    {
        public Guid Id { get; set; }
    }
}

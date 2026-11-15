using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageVouchers.Commands.ToggleLockVoucher
{
    public sealed class ToggleLockVoucherHandler : IRequestHandler<ToggleLockVoucherCommand, bool>
    {
        private readonly IVoucherOperations _operations;

        public ToggleLockVoucherHandler(IVoucherOperations operations)
        {
            _operations = operations;
        }

        public async Task<bool> Handle(ToggleLockVoucherCommand request, CancellationToken cancellationToken)
        {
            return await _operations.ToggleLockVoucherAsync(request.Id, cancellationToken);
        }
    }
}

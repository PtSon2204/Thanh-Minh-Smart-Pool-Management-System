using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageVouchers.Commands.DeleteVoucher
{
    public sealed class DeleteVoucherHandler : IRequestHandler<DeleteVoucherCommand, bool>
    {
        private readonly IVoucherOperations _operations;

        public DeleteVoucherHandler(IVoucherOperations operations)
        {
            _operations = operations;
        }

        public async Task<bool> Handle(DeleteVoucherCommand request, CancellationToken cancellationToken)
        {
            return await _operations.DeleteVoucherAsync(request.Id, cancellationToken);
        }
    }
}

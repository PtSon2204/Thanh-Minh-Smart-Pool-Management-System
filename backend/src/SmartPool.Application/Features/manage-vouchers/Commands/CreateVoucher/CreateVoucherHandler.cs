using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher
{
    public sealed class CreateVoucherHandler : IRequestHandler<CreateVoucherCommand, CreateVoucherResponse>
    {
        private readonly IVoucherOperations operations;
        private readonly IValidator<CreateVoucherCommand> validator;

        public CreateVoucherHandler(IVoucherOperations operations, IValidator<CreateVoucherCommand> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<CreateVoucherResponse> Handle(CreateVoucherCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.CreateVoucherAsync(request, cancellationToken);
        }
    }
}

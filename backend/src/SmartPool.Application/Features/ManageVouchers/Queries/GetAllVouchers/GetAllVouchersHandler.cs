using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers
{
    public sealed class GetAllVouchersHandler : IRequestHandler<GetAllVouchersQuery, PagedResponse<GetVouchersResponse>>
    {
        private readonly IVoucherOperations operations;
        private readonly IValidator<GetAllVouchersQuery> validator;

        public GetAllVouchersHandler(IVoucherOperations operations, IValidator<GetAllVouchersQuery> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<PagedResponse<GetVouchersResponse>> Handle(GetAllVouchersQuery request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.GetAllVouchersAsync(request, cancellationToken);
        }
    }
}

using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetSalaries
{
    public sealed class GetSalariesHandler : IRequestHandler<GetSalariesQuery, PagedResponse<GetSalariesResponse>>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<GetSalariesQuery> validator;

        public GetSalariesHandler(IStaffOperations operations, IValidator<GetSalariesQuery> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<PagedResponse<GetSalariesResponse>> Handle(GetSalariesQuery request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.GetSalariesAsync(request, cancellationToken);
        }
    }
}

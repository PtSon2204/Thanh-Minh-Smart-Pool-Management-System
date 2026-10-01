using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffOptions
{
    public sealed class GetStaffOptionsHandler : IRequestHandler<GetStaffOptionsQuery, GetStaffOptionsResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<GetStaffOptionsQuery> validator;

        public GetStaffOptionsHandler(IStaffOperations operations, IValidator<GetStaffOptionsQuery> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<GetStaffOptionsResponse> Handle(GetStaffOptionsQuery request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.GetOptionsAsync(cancellationToken);
        }
    }
}

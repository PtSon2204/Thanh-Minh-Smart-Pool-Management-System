using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffs
{
    public sealed class GetStaffsHandler : IRequestHandler<GetStaffsQuery, PagedResponse<GetStaffsResponse>>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<GetStaffsQuery> validator;

        public GetStaffsHandler(IStaffOperations operations, IValidator<GetStaffsQuery> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<PagedResponse<GetStaffsResponse>> Handle(GetStaffsQuery request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.GetStaffsAsync(request, cancellationToken);
        }
    }
}

using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetShifts
{
    public sealed class GetShiftsHandler : IRequestHandler<GetShiftsQuery, PagedResponse<GetShiftsResponse>>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<GetShiftsQuery> validator;

        public GetShiftsHandler(IStaffOperations operations, IValidator<GetShiftsQuery> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<PagedResponse<GetShiftsResponse>> Handle(GetShiftsQuery request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.GetShiftsAsync(request, cancellationToken);
        }
    }
}

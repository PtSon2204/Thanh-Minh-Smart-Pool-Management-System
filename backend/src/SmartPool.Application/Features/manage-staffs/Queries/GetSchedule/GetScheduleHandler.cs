using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule
{
    public sealed class GetScheduleHandler : IRequestHandler<GetScheduleQuery, PagedResponse<GetScheduleResponse>>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<GetScheduleQuery> validator;

        public GetScheduleHandler(IStaffOperations operations, IValidator<GetScheduleQuery> validator)
        {
            this.operations = operations;
            this.validator = validator;
        }

        public async Task<PagedResponse<GetScheduleResponse>> Handle(GetScheduleQuery request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            return await operations.GetScheduleAsync(request, cancellationToken);
        }
    }
}

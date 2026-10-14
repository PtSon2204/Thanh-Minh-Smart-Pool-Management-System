using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetScheduleEmployees
{
    public sealed class GetScheduleEmployeesHandler : IRequestHandler<GetScheduleEmployeesQuery, IReadOnlyList<GetScheduleEmployeeResponse>>
    {
        private readonly IStaffOperations operations;

        public GetScheduleEmployeesHandler(IStaffOperations operations)
        {
            this.operations = operations;
        }

        public Task<IReadOnlyList<GetScheduleEmployeeResponse>> Handle(GetScheduleEmployeesQuery request, CancellationToken cancellationToken)
        {
            return operations.GetScheduleEmployeesAsync(cancellationToken);
        }
    }
}

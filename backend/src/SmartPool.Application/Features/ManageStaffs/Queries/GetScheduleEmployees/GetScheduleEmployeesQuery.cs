using MediatR;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetScheduleEmployees
{
    public sealed class GetScheduleEmployeesQuery : IRequest<IReadOnlyList<GetScheduleEmployeeResponse>>;
}

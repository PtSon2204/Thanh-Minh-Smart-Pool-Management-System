using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetAttendanceHistory;

public sealed class GetAttendanceHistoryHandler : IRequestHandler<GetAttendanceHistoryQuery, PagedResponse<GetAttendanceHistoryResponse>>
{
    private readonly IStaffOperations operations;
    private readonly IValidator<GetAttendanceHistoryQuery> validator;

    public GetAttendanceHistoryHandler(IStaffOperations operations, IValidator<GetAttendanceHistoryQuery> validator)
    {
        this.operations = operations;
        this.validator = validator;
    }

    public async Task<PagedResponse<GetAttendanceHistoryResponse>> Handle(GetAttendanceHistoryQuery request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.GetAttendanceHistoryAsync(request, cancellationToken);
    }
}

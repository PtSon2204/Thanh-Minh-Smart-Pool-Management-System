using MediatR;
using SmartPool.Application.Features.ManageStaffs.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries;

public sealed record GetStaffsQuery(StaffListRequest Request) : IRequest<OperationResult<PageResult<StaffDto>>>;
public sealed class GetStaffsHandler(IStaffOperations operations) : IRequestHandler<GetStaffsQuery, OperationResult<PageResult<StaffDto>>>
{ public Task<OperationResult<PageResult<StaffDto>>> Handle(GetStaffsQuery query, CancellationToken cancellationToken) => operations.GetStaffsAsync(query.Request, cancellationToken); }
public sealed record GetStaffOptionsQuery : IRequest<OperationResult<StaffOptionsDto>>;
public sealed class GetStaffOptionsHandler(IStaffOperations operations) : IRequestHandler<GetStaffOptionsQuery, OperationResult<StaffOptionsDto>>
{ public Task<OperationResult<StaffOptionsDto>> Handle(GetStaffOptionsQuery query, CancellationToken cancellationToken) => operations.GetOptionsAsync(cancellationToken); }
public sealed record GetShiftsQuery(ShiftListRequest Request) : IRequest<OperationResult<PageResult<ShiftDto>>>;
public sealed class GetShiftsHandler(IStaffOperations operations) : IRequestHandler<GetShiftsQuery, OperationResult<PageResult<ShiftDto>>>
{ public Task<OperationResult<PageResult<ShiftDto>>> Handle(GetShiftsQuery query, CancellationToken cancellationToken) => operations.GetShiftsAsync(query.Request, cancellationToken); }

using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageStaffs.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries;

public sealed record GetScheduleQuery(ScheduleListRequest Request) : IRequest<OperationResult<PageResult<ScheduleDto>>>;
public sealed class GetScheduleHandler(IStaffOperations operations) : IRequestHandler<GetScheduleQuery, OperationResult<PageResult<ScheduleDto>>>
{ public Task<OperationResult<PageResult<ScheduleDto>>> Handle(GetScheduleQuery query, CancellationToken cancellationToken) => operations.GetScheduleAsync(query.Request, cancellationToken); }
public sealed record GetSalariesQuery(SalaryListRequest Request) : IRequest<OperationResult<PageResult<SalaryDto>>>;
public sealed class GetSalariesValidator : AbstractValidator<GetSalariesQuery>
{
    public GetSalariesValidator()
    {
        RuleFor(query => query.Request).Must(request => request.Month is null or >= 1 and <= 12).WithName(nameof(SalaryListRequest.Month)).WithMessage("Month must be between 1 and 12.");
        RuleFor(query => query.Request).Must(request => request.Year is null or >= 1 and <= 9999).WithName(nameof(SalaryListRequest.Year)).WithMessage("Year must be between 1 and 9999.");
    }
}
public sealed class GetSalariesHandler(IStaffOperations operations, IValidator<GetSalariesQuery> validator) : IRequestHandler<GetSalariesQuery, OperationResult<PageResult<SalaryDto>>>
{
    public async Task<OperationResult<PageResult<SalaryDto>>> Handle(GetSalariesQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        return validation.IsValid
            ? await operations.GetSalariesAsync(query.Request, cancellationToken)
            : OperationResult<PageResult<SalaryDto>>.Failure(StaffError.Validation, validation.Errors[0].ErrorMessage);
    }
}

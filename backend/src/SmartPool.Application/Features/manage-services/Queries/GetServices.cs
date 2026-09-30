using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageServices.Commands;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Queries;

public sealed record GetServicesQuery(ServiceListQuery Request) : IRequest<ServiceOperationResult<PageDto<ServiceDto>>>;
public sealed class GetServicesValidator : AbstractValidator<GetServicesQuery>
{
    public GetServicesValidator()
    {
        RuleFor(query => query.Request.Page).GreaterThan(0);
        RuleFor(query => query.Request.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class GetServicesHandler(IServiceOperations operations, IValidator<GetServicesQuery> validator) : IRequestHandler<GetServicesQuery, ServiceOperationResult<PageDto<ServiceDto>>>
{
    public async Task<ServiceOperationResult<PageDto<ServiceDto>>> Handle(GetServicesQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        return validation.IsValid ? await operations.GetServicesAsync(query.Request, cancellationToken) : ValidationResult<PageDto<ServiceDto>>.From(validation);
    }
}

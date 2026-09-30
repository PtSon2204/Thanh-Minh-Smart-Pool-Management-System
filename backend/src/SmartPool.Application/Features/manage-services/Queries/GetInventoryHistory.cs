using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageServices.Commands;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Queries;

public sealed record GetInventoryHistoryQuery(Guid ServiceId, InventoryHistoryQuery Request) : IRequest<ServiceOperationResult<PageDto<InventoryLogDto>>>;
public sealed class GetInventoryHistoryValidator : AbstractValidator<GetInventoryHistoryQuery>
{
    public GetInventoryHistoryValidator()
    {
        RuleFor(query => query.ServiceId).NotEmpty();
        RuleFor(query => query.Request.Page).GreaterThan(0);
        RuleFor(query => query.Request.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class GetInventoryHistoryHandler(IServiceOperations operations, IValidator<GetInventoryHistoryQuery> validator) : IRequestHandler<GetInventoryHistoryQuery, ServiceOperationResult<PageDto<InventoryLogDto>>>
{
    public async Task<ServiceOperationResult<PageDto<InventoryLogDto>>> Handle(GetInventoryHistoryQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        return validation.IsValid ? await operations.GetInventoryHistoryAsync(query.ServiceId, query.Request, cancellationToken) : ValidationResult<PageDto<InventoryLogDto>>.From(validation);
    }
}

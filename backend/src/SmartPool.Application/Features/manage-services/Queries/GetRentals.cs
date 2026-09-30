using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageServices.Commands;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Queries;

public sealed record GetRentalsQuery(RentalListQuery Request) : IRequest<ServiceOperationResult<PageDto<RentalDto>>>;
public sealed class GetRentalsValidator : AbstractValidator<GetRentalsQuery>
{
    public GetRentalsValidator()
    {
        RuleFor(query => query.Request.Page).GreaterThan(0);
        RuleFor(query => query.Request.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class GetRentalsHandler(IServiceOperations operations, IValidator<GetRentalsQuery> validator) : IRequestHandler<GetRentalsQuery, ServiceOperationResult<PageDto<RentalDto>>>
{
    public async Task<ServiceOperationResult<PageDto<RentalDto>>> Handle(GetRentalsQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        return validation.IsValid ? await operations.GetRentalsAsync(query.Request, cancellationToken) : ValidationResult<PageDto<RentalDto>>.From(validation);
    }
}

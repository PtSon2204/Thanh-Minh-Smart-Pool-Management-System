using FluentValidation;
using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.AccessControlPool.Queries;

public sealed record GetEntryHistoryQuery(int Page, int PageSize, DateOnly? Date, string? Status, Guid? TicketId)
    : IRequest<ValidationResult<PagedResult<EntryHistoryItemDto>>>;

public sealed class GetEntryHistoryValidator : AbstractValidator<GetEntryHistoryQuery>
{
    public GetEntryHistoryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query).Must(query => ((long)query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithName(nameof(GetEntryHistoryQuery.Page))
            .WithMessage("Page and pageSize produce an unsupported offset.");
        RuleFor(query => query.Date).Must(date => date is null || date.Value < DateOnly.MaxValue)
            .WithMessage("Date must allow a following local day.");
        RuleFor(query => query.Status).Must(status => status is null or PoolAccessValues.Allowed or PoolAccessValues.Denied);
    }
}

public sealed class GetEntryHistoryHandler(
    IPoolAccessOperations operations,
    IValidator<GetEntryHistoryQuery> validator)
    : IRequestHandler<GetEntryHistoryQuery, ValidationResult<PagedResult<EntryHistoryItemDto>>>
{
    public async Task<ValidationResult<PagedResult<EntryHistoryItemDto>>> Handle(
        GetEntryHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var failure = validation.Errors[0];
            return ValidationResult<PagedResult<EntryHistoryItemDto>>.Failure(failure.PropertyName, failure.ErrorMessage);
        }

        var filter = new EntryHistoryFilter(request.Page, request.PageSize, request.Date, request.Status, request.TicketId);
        var result = await operations.GetHistoryAsync(filter, cancellationToken);
        return ValidationResult<PagedResult<EntryHistoryItemDto>>.Success(result);
    }
}

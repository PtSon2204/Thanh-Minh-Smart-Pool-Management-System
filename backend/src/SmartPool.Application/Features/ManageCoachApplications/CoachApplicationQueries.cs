using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageCoachApplications;

public sealed record GetMyCoachApplicationQuery : IRequest<CoachApplicationItem?>;
public sealed record GetMyCoachApplicationRequestsQuery(int PageIndex = 1, int PageSize = 10)
    : IRequest<PagedResponse<CoachApplicationItem>>;
public sealed record GetCoachApplicationsQuery(string? Status, int PageIndex = 1, int PageSize = 20)
    : IRequest<PagedResponse<CoachApplicationItem>>;
public sealed record GetCoachApplicationByIdQuery(Guid Id) : IRequest<CoachApplicationItem?>;
public sealed record GetCoachCertificateQuery(Guid Id) : IRequest<CoachCertificateAccess?>;

public sealed class GetMyCoachApplicationRequestsValidator
    : AbstractValidator<GetMyCoachApplicationRequestsQuery>
{
    public GetMyCoachApplicationRequestsValidator()
    {
        RuleFor(query => query.PageIndex).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetCoachApplicationsValidator : AbstractValidator<GetCoachApplicationsQuery>
{
    public GetCoachApplicationsValidator()
    {
        RuleFor(query => query.PageIndex).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => string.IsNullOrWhiteSpace(status) ||
                new[] { "Pending", "Approved", "Rejected", "Revoked" }
                    .Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Bộ lọc trạng thái không hợp lệ.");
    }
}

public sealed class GetMyCoachApplicationHandler(
    ICoachApplicationOperations operations, ICurrentUser currentUser)
    : IRequestHandler<GetMyCoachApplicationQuery, CoachApplicationItem?>
{
    public Task<CoachApplicationItem?> Handle(
        GetMyCoachApplicationQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || !currentUser.IsInRole("CUSTOMER"))
            throw new UnauthorizedAccessException("Chỉ Customer được xem yêu cầu Coach của mình.");
        return operations.GetLatestForUserAsync(userId, cancellationToken);
    }
}

public sealed class GetMyCoachApplicationRequestsHandler(
    ICoachApplicationOperations operations, ICurrentUser currentUser,
    IValidator<GetMyCoachApplicationRequestsQuery> validator)
    : IRequestHandler<GetMyCoachApplicationRequestsQuery, PagedResponse<CoachApplicationItem>>
{
    public async Task<PagedResponse<CoachApplicationItem>> Handle(
        GetMyCoachApplicationRequestsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || !currentUser.IsInRole("CUSTOMER"))
            throw new UnauthorizedAccessException("Chỉ Customer được xem yêu cầu Coach của mình.");
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.GetListForUserAsync(userId, request.PageIndex,
            request.PageSize, cancellationToken);
    }
}

public sealed class GetCoachApplicationsHandler(
    ICoachApplicationOperations operations, ICurrentUser currentUser,
    IValidator<GetCoachApplicationsQuery> validator)
    : IRequestHandler<GetCoachApplicationsQuery, PagedResponse<CoachApplicationItem>>
{
    public async Task<PagedResponse<CoachApplicationItem>> Handle(
        GetCoachApplicationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null || !currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Admin được xem tất cả yêu cầu Coach.");
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.GetListAsync(request.Status, request.PageIndex,
            request.PageSize, cancellationToken);
    }
}

public sealed class GetCoachApplicationByIdHandler(
    ICoachApplicationOperations operations, ICurrentUser currentUser)
    : IRequestHandler<GetCoachApplicationByIdQuery, CoachApplicationItem?>
{
    public Task<CoachApplicationItem?> Handle(
        GetCoachApplicationByIdQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            throw new UnauthorizedAccessException("Cần đăng nhập để xem yêu cầu Coach.");
        if (currentUser.IsInRole("ADMIN"))
            return operations.GetByIdAsync(request.Id, cancellationToken);
        if (!currentUser.IsInRole("CUSTOMER"))
            throw new UnauthorizedAccessException("Không có quyền xem yêu cầu Coach.");
        return operations.GetByIdForUserAsync(request.Id, userId, cancellationToken);
    }
}

public sealed class GetCoachCertificateHandler(
    ICoachApplicationOperations operations, ICurrentUser currentUser)
    : IRequestHandler<GetCoachCertificateQuery, CoachCertificateAccess?>
{
    public Task<CoachCertificateAccess?> Handle(
        GetCoachCertificateQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId ||
            (!currentUser.IsInRole("ADMIN") && !currentUser.IsInRole("CUSTOMER")))
            throw new UnauthorizedAccessException("Không có quyền xem chứng chỉ Coach.");
        return operations.GetCertificateAsync(request.Id, userId,
            currentUser.IsInRole("ADMIN"), cancellationToken);
    }
}

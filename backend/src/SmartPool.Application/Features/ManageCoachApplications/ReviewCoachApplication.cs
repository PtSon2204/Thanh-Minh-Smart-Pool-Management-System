using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageCoachApplications;

public sealed record ReviewCoachApplicationCommand(Guid Id, string? Decision, string? Note)
    : IRequest<CoachApplicationItem?>;

public sealed class ReviewCoachApplicationValidator : AbstractValidator<ReviewCoachApplicationCommand>
{
    public ReviewCoachApplicationValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Decision)
            .Must(decision => decision is not null &&
                (decision.Trim().Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
                 decision.Trim().Equals("Rejected", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Quyết định phải là Approved hoặc Rejected.");
        RuleFor(command => command.Note)
            .MaximumLength(1000).WithMessage("Ghi chú tối đa 1000 ký tự.")
            .Must((command, note) =>
                !string.Equals(command.Decision?.Trim(), "Rejected", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(note))
            .WithMessage("Cần ghi lý do từ chối.");
    }
}

public sealed class ReviewCoachApplicationHandler(
    ICoachApplicationOperations operations,
    ICurrentUser currentUser,
    IValidator<ReviewCoachApplicationCommand> validator)
    : IRequestHandler<ReviewCoachApplicationCommand, CoachApplicationItem?>
{
    public async Task<CoachApplicationItem?> Handle(
        ReviewCoachApplicationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId || !currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Admin được duyệt yêu cầu Coach.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var decision = request.Decision!.Trim().Equals("Approved", StringComparison.OrdinalIgnoreCase)
            ? "Approved" : "Rejected";
        return await operations.ReviewAsync(request.Id, adminId, decision,
            request.Note, cancellationToken);
    }
}

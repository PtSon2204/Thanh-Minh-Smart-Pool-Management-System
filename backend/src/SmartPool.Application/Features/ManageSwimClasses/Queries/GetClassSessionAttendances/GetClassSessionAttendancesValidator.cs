using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassSessionAttendances;

public sealed class GetClassSessionAttendancesValidator
    : AbstractValidator<GetClassSessionAttendancesQuery>
{
    private static readonly string[] AllowedStatuses = ["PendingReview", "Verified", "Rejected"];

    public GetClassSessionAttendancesValidator()
    {
        RuleFor(query => query.ClassId).NotEmpty();
        RuleFor(query => query.SessionId).NotEmpty();
        RuleFor(query => query.PageIndex).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => string.IsNullOrWhiteSpace(status) ||
                AllowedStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Trạng thái điểm danh không hợp lệ.");
    }
}

namespace SmartPool.Application.Features.ManageSwimClasses;

public sealed record ClassAttendanceResponse(
    Guid Id,
    Guid ClassId,
    Guid SessionId,
    Guid RegistrationId,
    string? ImageUrl,
    string Status,
    DateTime SubmittedAt,
    DateTime UpdatedAt,
    string? ProgressNote = null);

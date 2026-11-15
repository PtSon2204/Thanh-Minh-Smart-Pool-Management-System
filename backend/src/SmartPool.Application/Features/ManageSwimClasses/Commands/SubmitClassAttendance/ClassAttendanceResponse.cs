namespace SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;

public sealed record ClassAttendanceResponse(
    Guid Id,
    Guid ClassId,
    Guid SessionId,
    Guid RegistrationId,
    string ImageUrl,
    string Status,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNote,
    DateTime UpdatedAt);

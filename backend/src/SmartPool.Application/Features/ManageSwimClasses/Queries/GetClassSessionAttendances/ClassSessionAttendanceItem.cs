namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassSessionAttendances;

public sealed record ClassSessionAttendanceItem(
    Guid AttendanceId,
    Guid RegistrationId,
    Guid CustomerUserId,
    string CustomerName,
    string ImageUrl,
    string Status,
    DateTime SubmittedAt,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? ReviewNote,
    DateTime UpdatedAt);

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;

public sealed record ReviewClassAttendanceResponse(
    Guid AttendanceId,
    Guid ClassId,
    Guid SessionId,
    string Status,
    Guid ReviewedByUserId,
    DateTime ReviewedAt,
    string? ReviewNote,
    DateTime UpdatedAt);

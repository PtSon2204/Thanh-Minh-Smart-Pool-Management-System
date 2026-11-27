using SmartPool.Application.Features.ManageSwimClasses;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassAttendances;

public sealed record MyClassAttendanceItem(
    Guid SessionId,
    DateTime StartsAt,
    DateTime EndsAt,
    string SessionStatus,
    string RegistrationStatus,
    bool EligibleForAttendance,
    ClassAttendanceResponse? Attendance);

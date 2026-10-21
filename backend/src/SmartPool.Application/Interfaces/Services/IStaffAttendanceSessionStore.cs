using SmartPool.Application.Features.ManageStaffs.AttendanceQr;

namespace SmartPool.Application.Interfaces.Services;

public interface IStaffAttendanceSessionStore
{
    Task<StaffAttendanceQrIssuedSession> CreateAsync(StaffAttendanceQrIssueRequest request, Guid issuerId, CancellationToken cancellationToken);
    Task<StaffAttendanceQrIssuedSession> RotateAsync(Guid sessionId, long expectedGeneration, Guid issuerId, CancellationToken cancellationToken);
    Task StopAsync(Guid sessionId, Guid issuerId, CancellationToken cancellationToken);
    Task<StaffAttendanceQrResolution?> ResolveAsync(string qrPayload, Guid employeeId, CancellationToken cancellationToken);
}

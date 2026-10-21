using System.Text.Json.Serialization;
using MediatR;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;

public sealed class ManualAttendanceCorrectionCommand : IRequest<RecordAttendanceResponse>
{
    [JsonIgnore]
    public Guid AssignmentId { get; set; }

    [JsonIgnore]
    public Guid PerformedByUserId { get; set; }

    public bool UpdateCheckIn { get; set; }
    public DateTimeOffset? CheckInTime { get; set; }
    public bool UpdateCheckOut { get; set; }
    public DateTimeOffset? CheckOutTime { get; set; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; set; }
    public string? Reason { get; set; }
}

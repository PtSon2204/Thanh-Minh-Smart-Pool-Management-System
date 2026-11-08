using System.Text.Json.Serialization;
using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;

public sealed class ReviewClassAttendanceCommand : IRequest<ReviewClassAttendanceResponse>
{
    [JsonIgnore]
    public Guid ClassId { get; set; }

    [JsonIgnore]
    public Guid SessionId { get; set; }

    [JsonIgnore]
    public Guid AttendanceId { get; set; }

    public string Decision { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }

    [JsonConverter(typeof(OffsetRequiredDateTimeOffsetConverter))]
    public DateTimeOffset ExpectedUpdatedAt { get; set; }
}

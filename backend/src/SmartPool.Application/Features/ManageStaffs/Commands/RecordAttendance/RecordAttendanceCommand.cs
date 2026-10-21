using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance
{
    public sealed class RecordAttendanceCommand : IRequest<RecordAttendanceResponse>
    {
        [JsonIgnore]
        public Guid Id { get; set; }
        [JsonIgnore]
        public bool CheckIn { get; set; }
        [JsonIgnore]
        public Guid? OwnerId { get; set; }
        public string? Reason { get; set; }
        [JsonIgnore]
        public string Method { get; set; } = string.Empty;
        [JsonIgnore]
        public Guid PerformedByUserId { get; set; }
    }
}

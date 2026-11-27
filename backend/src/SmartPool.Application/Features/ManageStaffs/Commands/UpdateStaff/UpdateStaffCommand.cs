using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff
{
    public sealed class UpdateStaffCommand : IRequest<UpdateStaffResponse>
    {
        [JsonIgnore]
        public Guid UserId { get; set; }
        [JsonIgnore]
        public Guid ActorId { get; set; }
        public string Status { get; set; } = string.Empty;
        public StaffEditDetails? Details { get; set; }

        private readonly HashSet<string> snapshotFields = [];
        private DateTime? expectedUpdatedAt, expectedUserUpdatedAt, expectedProfileUpdatedAt;
        private long? expectedLegalProfileVersion;
        private bool? expectedProfileExists;

        public DateTime? ExpectedUpdatedAt { get => expectedUpdatedAt; set { expectedUpdatedAt = value; snapshotFields.Add(nameof(ExpectedUpdatedAt)); } }
        public DateTime? ExpectedUserUpdatedAt { get => expectedUserUpdatedAt; set { expectedUserUpdatedAt = value; snapshotFields.Add(nameof(ExpectedUserUpdatedAt)); } }
        public DateTime? ExpectedProfileUpdatedAt { get => expectedProfileUpdatedAt; set { expectedProfileUpdatedAt = value; snapshotFields.Add(nameof(ExpectedProfileUpdatedAt)); } }
        public bool? ExpectedProfileExists { get => expectedProfileExists; set { expectedProfileExists = value; snapshotFields.Add(nameof(ExpectedProfileExists)); } }
        public long? ExpectedLegalProfileVersion { get => expectedLegalProfileVersion; set { expectedLegalProfileVersion = value; snapshotFields.Add(nameof(ExpectedLegalProfileVersion)); } }

        [JsonIgnore]
        public bool HasEditSnapshot => snapshotFields.Count == 5 && ExpectedProfileExists.HasValue;
    }
}

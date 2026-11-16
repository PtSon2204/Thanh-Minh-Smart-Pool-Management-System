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
        private Guid? expectedApprovedRevisionId;
        private long? expectedApprovedRevisionVersion, expectedCorrectionVersion;
        private bool? expectedProfileExists;

        public DateTime? ExpectedUpdatedAt { get => expectedUpdatedAt; set { expectedUpdatedAt = value; snapshotFields.Add(nameof(ExpectedUpdatedAt)); } }
        public DateTime? ExpectedUserUpdatedAt { get => expectedUserUpdatedAt; set { expectedUserUpdatedAt = value; snapshotFields.Add(nameof(ExpectedUserUpdatedAt)); } }
        public DateTime? ExpectedProfileUpdatedAt { get => expectedProfileUpdatedAt; set { expectedProfileUpdatedAt = value; snapshotFields.Add(nameof(ExpectedProfileUpdatedAt)); } }
        public bool? ExpectedProfileExists { get => expectedProfileExists; set { expectedProfileExists = value; snapshotFields.Add(nameof(ExpectedProfileExists)); } }
        public Guid? ExpectedApprovedRevisionId { get => expectedApprovedRevisionId; set { expectedApprovedRevisionId = value; snapshotFields.Add(nameof(ExpectedApprovedRevisionId)); } }
        public long? ExpectedApprovedRevisionVersion { get => expectedApprovedRevisionVersion; set { expectedApprovedRevisionVersion = value; snapshotFields.Add(nameof(ExpectedApprovedRevisionVersion)); } }
        public long? ExpectedCorrectionVersion { get => expectedCorrectionVersion; set { expectedCorrectionVersion = value; snapshotFields.Add(nameof(ExpectedCorrectionVersion)); } }

        [JsonIgnore]
        public bool HasEditSnapshot => snapshotFields.Count == 7 && ExpectedProfileExists.HasValue && ExpectedCorrectionVersion.HasValue;
    }
}

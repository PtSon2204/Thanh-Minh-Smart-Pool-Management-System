using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment
{
    public sealed class CancelShiftAssignmentCommand : IRequest<CancelShiftAssignmentResponse>
    {
        [JsonIgnore]
        public Guid Id { get; set; }
    }
}

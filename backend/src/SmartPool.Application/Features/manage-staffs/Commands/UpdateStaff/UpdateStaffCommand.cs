using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff
{
    public sealed class UpdateStaffCommand : IRequest<UpdateStaffResponse>
    {
        [JsonIgnore]
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
        public decimal BaseSalary { get; set; }
        public string Status { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Address { get; set; }
        public DateOnly? DateOfBirth { get; set; }
    }
}

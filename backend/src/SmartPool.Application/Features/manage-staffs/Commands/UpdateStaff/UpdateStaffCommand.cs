using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff
{
    public sealed class UpdateStaffCommand : IRequest<UpdateStaffResponse>
    {
        [JsonIgnore]
        public Guid UserId { get; set; }
        public decimal BaseSalary { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}

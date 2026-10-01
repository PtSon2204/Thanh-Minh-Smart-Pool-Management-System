using MediatR;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff
{
    public sealed class CreateStaffCommand : IRequest<CreateStaffResponse>
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
        public DateOnly JoinDate { get; set; }
        public decimal BaseSalary { get; set; }
        public string Status { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Address { get; set; }
        public DateOnly? DateOfBirth { get; set; }
    }
}

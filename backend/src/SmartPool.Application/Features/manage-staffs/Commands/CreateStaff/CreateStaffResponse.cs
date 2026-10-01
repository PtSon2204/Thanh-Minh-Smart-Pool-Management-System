namespace SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff
{
    public sealed class CreateStaffResponse
    {
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Address { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public DateOnly JoinDate { get; set; }
        public decimal BaseSalary { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}

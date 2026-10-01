namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffOptions
{
    public sealed class GetStaffOptionsResponse
    {
        public List<EligibleUserResponse> Users { get; set; } = new();
        public List<RoleResponse> Roles { get; set; } = new();
    }

    public sealed class EligibleUserResponse
    {
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? FullName { get; set; }
    }

    public sealed class RoleResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

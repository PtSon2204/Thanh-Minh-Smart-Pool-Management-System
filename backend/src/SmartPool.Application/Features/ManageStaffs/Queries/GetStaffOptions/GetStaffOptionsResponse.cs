namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffOptions
{
    public sealed class GetStaffOptionsResponse
    {
        public List<EligibleUserResponse> Users { get; set; } = new();
    }
}

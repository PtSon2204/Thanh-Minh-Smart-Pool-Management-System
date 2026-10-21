namespace SmartPool.Application.Features.ManageStaffs
{

    public sealed class StaffForbiddenException : Exception
    {
        public StaffForbiddenException(string message) : base(message)
        {
        }
    }
}

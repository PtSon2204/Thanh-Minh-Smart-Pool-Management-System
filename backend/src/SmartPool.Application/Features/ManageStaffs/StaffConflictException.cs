namespace SmartPool.Application.Features.ManageStaffs
{
    public sealed class StaffConflictException : Exception
    {
        public StaffConflictException(string message) : base(message)
        {
        }
    }
}

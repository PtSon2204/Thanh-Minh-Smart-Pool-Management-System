namespace SmartPool.Application.Features.ManageStaffs
{
    public sealed class StaffConflictException : Exception
    {
        public StaffConflictException(string message) : base(message)
        {
        }
    }

    public sealed class StaffForbiddenException : Exception
    {
        public StaffForbiddenException(string message) : base(message)
        {
        }
    }
}

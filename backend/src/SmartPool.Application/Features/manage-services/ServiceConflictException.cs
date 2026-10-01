namespace SmartPool.Application.Features.ManageServices
{
    public sealed class ServiceConflictException : Exception
    {
        public ServiceConflictException(string message) : base(message)
        {
        }
    }
}

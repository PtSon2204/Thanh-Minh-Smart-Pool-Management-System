namespace SmartPool.Application.Interfaces.Services;

public interface ICameraRegistration : IAsyncDisposable
{
    string Identity { get; }
    string ProtectedConfiguration { get; }
    void Publish(Guid equipmentId);
    void Confirm();
}

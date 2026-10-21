namespace SmartPool.Application.Features.ManageEquipments;

public sealed class CameraConnectionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

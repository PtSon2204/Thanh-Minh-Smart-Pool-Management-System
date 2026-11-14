namespace SmartPool.Application.Features.ManageEquipments.Cameras;
public sealed record CameraOffer(string Type, string Sdp, bool Analysis = false, bool Smooth = false);

using MediatR;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.CreateWaterQualityLog;

public sealed class CreateWaterQualityLogCommand : IRequest<WaterQualityLogDto>
{
    public DateTime InspectionTime { get; set; } = DateTime.UtcNow;
    public decimal PhValue { get; set; }
    public decimal FreeChlorineMgL { get; set; }
    public bool? IsWaterClear { get; set; }
    public Guid? EquipmentId { get; set; }
    public string? Notes { get; set; }
}

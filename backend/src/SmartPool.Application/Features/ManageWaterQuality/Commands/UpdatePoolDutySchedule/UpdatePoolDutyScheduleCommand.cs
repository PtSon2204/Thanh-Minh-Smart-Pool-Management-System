using MediatR;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;

public sealed class UpdatePoolDutyScheduleCommand : IRequest<PoolDutyScheduleDto>
{
    public Guid? Id { get; set; }
    public DateOnly DutyDate { get; set; }
    public Guid ShiftId { get; set; }
    public Guid AssignedEmployeeId { get; set; }
    public string Status { get; set; } = "Scheduled";
    public Guid? WaterQualityLogId { get; set; }
    public string? Notes { get; set; }
}


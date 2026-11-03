using MediatR;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;

public sealed class AutoAssignPoolDutyCommand : IRequest<AutoAssignPoolDutyResultDto>
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public List<Guid>? ShiftIds { get; set; }
    public List<Guid>? CandidateEmployeeIds { get; set; }
    public bool OverwriteExisting { get; set; }
    public bool PrioritizeShiftRoster { get; set; } = true;
    public string? DefaultNotes { get; set; }
}


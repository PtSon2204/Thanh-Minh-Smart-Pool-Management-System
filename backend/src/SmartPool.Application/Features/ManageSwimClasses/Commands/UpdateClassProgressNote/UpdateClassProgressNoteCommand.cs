using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassProgressNote;

public sealed class UpdateClassProgressNoteCommand : IRequest<ClassAttendanceResponse>
{
    public Guid ClassId { get; set; }
    public Guid SessionId { get; set; }
    public Guid RegistrationId { get; set; }
    public string? ProgressNote { get; set; }
    public DateTimeOffset? ExpectedUpdatedAt { get; set; }
}

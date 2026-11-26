using MediatR;
using Microsoft.AspNetCore.Http;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.RecordClassAttendance;

public sealed class RecordClassAttendanceCommand : IRequest<ClassAttendanceResponse>
{
    public Guid ClassId { get; set; }
    public Guid SessionId { get; set; }
    public Guid RegistrationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public IFormFile? Image { get; set; }
    public DateTimeOffset? ExpectedUpdatedAt { get; set; }
}

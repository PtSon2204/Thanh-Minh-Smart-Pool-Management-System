using MediatR;
using Microsoft.AspNetCore.Http;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;

public sealed class SubmitClassAttendanceCommand : IRequest<ClassAttendanceResponse>
{
    public Guid ClassId { get; set; }
    public Guid SessionId { get; set; }
    public IFormFile? Image { get; set; }
}

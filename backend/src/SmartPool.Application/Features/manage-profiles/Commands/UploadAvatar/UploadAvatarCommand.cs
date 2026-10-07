using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Profiles.Commands.UploadAvatar;

public sealed class UploadAvatarCommand : IRequest<IActionResult>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid UserId { get; set; }
    
    public IFormFile File { get; set; } = null!;
}

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.Profiles.Queries.GetMyProfile;
using SmartPool.Application.Features.Profiles.Commands.UpdateMyProfile;
using SmartPool.API.Extensions; // Assuming User.TryGetUserId is here or similar

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/profiles")]
[Authorize]
public sealed class ProfilesController : ControllerBase
{
    private readonly ISender _sender;

    public ProfilesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Lấy thông tin hồ sơ của người dùng đang đăng nhập.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(GetMyProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Forbid();
        
        try 
        {
            var response = await _sender.Send(new GetMyProfileQuery { UserId = userId }, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Cập nhật hồ sơ cá nhân.</summary>
    [HttpPut("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Forbid();
        
        command.UserId = userId;
        return await _sender.Send(command, cancellationToken);
    }

    /// <summary>Upload avatar</summary>
    [HttpPost("me/avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadAvatar([FromForm] SmartPool.Application.Features.Profiles.Commands.UploadAvatar.UploadAvatarCommand command, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Forbid();
        
        command.UserId = userId;
        return await _sender.Send(command, cancellationToken);
    }
}

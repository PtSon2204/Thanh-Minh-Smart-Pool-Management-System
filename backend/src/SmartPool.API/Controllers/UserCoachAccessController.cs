using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/coach-access")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class UserCoachAccessController(ICoachApplicationOperations applications) : ControllerBase
{
    public sealed record RevokeCoachRequest(string Reason);

    [HttpPost("revoke")]
    [RequirePermission("coach_applications", "review")]
    public async Task<IActionResult> Revoke(Guid userId, [FromBody] RevokeCoachRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            return BadRequest(new { message = "Vui lòng nhập lý do thu hồi, tối đa 1000 ký tự." });
        try
        {
            var result = await applications.RevokeForUserAsync(userId, adminId,
                request.Reason, cancellationToken);
            return result is null ? NotFound(new { message = "Không có quyền Coach được cấp qua yêu cầu đang hoạt động." }) : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}

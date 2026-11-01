using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/coach-applications")]
[Authorize]
public sealed class CoachApplicationsController(ICoachApplicationOperations applications) : ControllerBase
{
    private const int MaxImageBytes = 5 * 1024 * 1024;

    [HttpPost]
    [Authorize(Roles = "CUSTOMER")]
    [RequirePermission("coach_applications", "create")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(CoachApplicationItem), StatusCodes.Status201Created)]
    public async Task<IActionResult> Submit(IFormFile? certificate,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Forbid();
        if (certificate is null || certificate.Length is < 4 or > MaxImageBytes ||
            certificate.ContentType is not ("image/jpeg" or "image/png"))
            return BadRequest(new { message = "Chỉ nhận ảnh JPG/PNG tối đa 5 MB." });
        await using var stream = certificate.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var image = buffer.ToArray();
        var format = DetectFormat(image);
        if (format is null || (format == "jpg" && certificate.ContentType != "image/jpeg") ||
            (format == "png" && certificate.ContentType != "image/png"))
            return BadRequest(new { message = "Nội dung ảnh không khớp định dạng JPG/PNG." });
        try
        {
            var result = await applications.SubmitAsync(userId, image, format, cancellationToken);
            return CreatedAtAction(nameof(GetMine), new { }, result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpGet("me")]
    [Authorize(Roles = "CUSTOMER")]
    [RequirePermission("coach_applications", "read")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Forbid();
        var result = await applications.GetLatestForUserAsync(userId, cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("me/requests")]
    [Authorize(Roles = "CUSTOMER")]
    [RequirePermission("coach_applications", "read")]
    [ProducesResponseType(typeof(PagedResponse<CoachApplicationItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyRequests([FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (!ValidPagination(pageIndex, pageSize))
            return BadRequest(new { message = "Phân trang không hợp lệ." });
        if (!User.TryGetUserId(out var userId)) return Forbid();
        return Ok(await applications.GetListForUserAsync(userId, pageIndex, pageSize,
            cancellationToken));
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("coach_applications", "read")]
    [ProducesResponseType(typeof(PagedResponse<CoachApplicationItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] string? status, [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!ValidPagination(pageIndex, pageSize) ||
            (!string.IsNullOrWhiteSpace(status) &&
             !new[] { "Pending", "Approved", "Rejected", "Revoked" }.Contains(status.Trim(),
                 StringComparer.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Bộ lọc hoặc phân trang không hợp lệ." });
        return Ok(await applications.GetListAsync(status, pageIndex, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("coach_applications", "read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Forbid();
        var result = User.HasRole(RoleEnum.ADMIN)
            ? await applications.GetByIdAsync(id, cancellationToken)
            : await applications.GetByIdForUserAsync(id, userId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/certificate")]
    public async Task<IActionResult> GetCertificate(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId)) return Forbid();
        try
        {
            var result = await applications.GetCertificateAsync(id, actorId,
                User.HasRole(RoleEnum.ADMIN), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    public sealed record ReviewRequest(string Decision, string? Note);

    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("coach_applications", "review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Forbid();
        var decision = request.Decision?.Trim();
        if (decision is null || !new[] { "Approved", "Rejected" }.Contains(decision,
                StringComparer.OrdinalIgnoreCase) || request.Note?.Length > 1000 ||
            (decision.Equals("Rejected", StringComparison.OrdinalIgnoreCase) &&
             string.IsNullOrWhiteSpace(request.Note)))
            return BadRequest(new { message = "Quyết định hoặc ghi chú không hợp lệ." });
        try
        {
            var result = await applications.ReviewAsync(id, adminId,
                decision.Equals("Approved", StringComparison.OrdinalIgnoreCase)
                    ? "Approved" : "Rejected", request.Note, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }


    private static string? DetectFormat(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        return bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 &&
            bytes[^2] == 0xFF && bytes[^1] == 0xD9 ? "jpg" : null;
    }

    private static bool ValidPagination(int pageIndex, int pageSize) =>
        pageIndex >= 1 && pageIndex <= int.MaxValue / 100 && pageSize is >= 1 and <= 100;
}

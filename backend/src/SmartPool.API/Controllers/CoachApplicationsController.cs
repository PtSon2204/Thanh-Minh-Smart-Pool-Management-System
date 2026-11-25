using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageCoachApplications;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/coach-applications")]
[Authorize]
public sealed class CoachApplicationsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "CUSTOMER")]
    [RequirePermission("coach_applications", "create")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(CoachApplicationItem), StatusCodes.Status201Created)]
    public Task<IActionResult> Submit(IFormFile? certificate, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => CreatedAtAction(nameof(GetMine), new { },
            await sender.Send(new SubmitCoachApplicationCommand(certificate), cancellationToken)));

    [HttpGet("me")]
    [Authorize(Roles = "CUSTOMER")]
    [RequirePermission("coach_applications", "read")]
    public Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await sender.Send(new GetMyCoachApplicationQuery(), cancellationToken);
            return result is null ? NoContent() : Ok(result);
        });

    [HttpGet("me/requests")]
    [Authorize(Roles = "CUSTOMER")]
    [RequirePermission("coach_applications", "read")]
    [ProducesResponseType(typeof(PagedResponse<CoachApplicationItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyRequests([FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await sender.Send(
            new GetMyCoachApplicationRequestsQuery(pageIndex, pageSize), cancellationToken)));

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("coach_applications", "read")]
    [ProducesResponseType(typeof(PagedResponse<CoachApplicationItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetList([FromQuery] string? status, [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await sender.Send(
            new GetCoachApplicationsQuery(status, pageIndex, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("coach_applications", "read")]
    public Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await sender.Send(new GetCoachApplicationByIdQuery(id), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        });

    [HttpGet("{id:guid}/certificate")]
    public Task<IActionResult> GetCertificate(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await sender.Send(new GetCoachCertificateQuery(id), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        });

    public sealed record ReviewRequest(string Decision, string? Note);

    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("coach_applications", "review")]
    public Task<IActionResult> Review(Guid id, [FromBody] ReviewRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await sender.Send(
                new ReviewCoachApplicationCommand(id, request.Decision, request.Note),
                cancellationToken);
            return result is null ? NotFound() : Ok(result);
        });

    private static async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> operation)
    {
        try
        {
            return await operation();
        }
        catch (ValidationException exception)
        {
            return new BadRequestObjectResult(new
            {
                message = exception.Errors.FirstOrDefault()?.ErrorMessage ??
                    "Dữ liệu yêu cầu Coach không hợp lệ.",
                errors = exception.Errors.GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray())
            });
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (InvalidOperationException exception)
        {
            return new ConflictObjectResult(new { message = exception.Message });
        }
    }
}

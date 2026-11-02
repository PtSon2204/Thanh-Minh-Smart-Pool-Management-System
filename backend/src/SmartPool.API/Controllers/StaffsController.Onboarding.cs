using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.API.Controllers;

public sealed partial class StaffsController
{
    [HttpGet("invitations"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "read")]
    public Task<IActionResult> Invitations([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default) =>
        TryGetUserId(out var actor) ? SendAsync(new ListStaffInvitationsQuery(page, pageSize) { ActorId = actor }, cancellationToken) : Task.FromResult<IActionResult>(Unauthorized());

    [HttpPost("invitations"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "create")]
    public Task<IActionResult> Invite([FromBody] OnboardingCommand command, CancellationToken cancellationToken) =>
        OnboardingAsync(command, OnboardingAction.Create, cancellationToken);

    [HttpGet("invitations/{id:guid}"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "read")]
    public Task<IActionResult> InvitationDetail(Guid id, CancellationToken cancellationToken) =>
        OnboardingAsync(new() { InvitationId = id }, OnboardingAction.Detail, cancellationToken);

    [HttpPost("invitations/{id:guid}/resend"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "update")]
    public Task<IActionResult> ResendInvitation(Guid id, [FromBody] OnboardingCommand command, CancellationToken cancellationToken)
    { command.InvitationId = id; return OnboardingAsync(command, OnboardingAction.Resend, cancellationToken); }

    [HttpPost("invitations/{id:guid}/cancel"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "update")]
    public Task<IActionResult> CancelInvitation(Guid id, [FromBody] OnboardingCommand command, CancellationToken cancellationToken)
    { command.InvitationId = id; return OnboardingAsync(command, OnboardingAction.Cancel, cancellationToken); }

    [HttpPost("onboarding/inspect"), AllowAnonymous]
    public Task<IActionResult> InspectOnboarding([FromBody] OnboardingCommand command, CancellationToken cancellationToken) =>
        OnboardingAsync(command, OnboardingAction.Inspect, cancellationToken, true);

    [HttpPost("onboarding/draft"), AllowAnonymous]
    public Task<IActionResult> SaveOnboardingDraft([FromBody] OnboardingCommand command, CancellationToken cancellationToken) =>
        OnboardingAsync(command, OnboardingAction.Draft, cancellationToken, true);

    [HttpPost("onboarding/submit"), AllowAnonymous]
    public Task<IActionResult> SubmitOnboarding([FromBody] OnboardingCommand command, CancellationToken cancellationToken) =>
        OnboardingAsync(command, OnboardingAction.Submit, cancellationToken, true);

    [HttpGet("me/onboarding"), Authorize]
    public Task<IActionResult> MyOnboarding(CancellationToken cancellationToken) => OnboardingAsync(new(), OnboardingAction.Own, cancellationToken);

    [HttpPost("me/onboarding/draft"), Authorize]
    public Task<IActionResult> SaveMyOnboarding([FromBody] OnboardingCommand command, CancellationToken cancellationToken) =>
        OwnOnboardingAsync(command, OnboardingAction.Draft, cancellationToken);

    [HttpPost("me/onboarding/submit"), Authorize]
    public Task<IActionResult> SubmitMyOnboarding([FromBody] OnboardingCommand command, CancellationToken cancellationToken) =>
        OwnOnboardingAsync(command, OnboardingAction.Submit, cancellationToken);

    [HttpPost("me/onboarding/correction-requests"), Authorize]
    public Task<IActionResult> RequestMyCorrection([FromBody] OnboardingCommand command, CancellationToken cancellationToken) =>
        OwnOnboardingAsync(command, OnboardingAction.RequestCorrection, cancellationToken);

    [HttpGet("{employeeId:guid}/onboarding"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "read")]
    public Task<IActionResult> EmployeeOnboarding(Guid employeeId, CancellationToken cancellationToken) =>
        OnboardingAsync(new() { EmployeeId = employeeId }, OnboardingAction.EmployeeDetail, cancellationToken);

    [HttpPost("{employeeId:guid}/onboarding/corrections"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "update")]
    public Task<IActionResult> OpenCorrection(Guid employeeId, [FromBody] OnboardingCommand command, CancellationToken cancellationToken)
    { command.EmployeeId = employeeId; return OnboardingAsync(command, OnboardingAction.OpenCorrection, cancellationToken); }

    [HttpPost("onboarding/revisions/{id:guid}/approve"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "update")]
    public Task<IActionResult> ApproveOnboarding(Guid id, [FromBody] OnboardingCommand command, CancellationToken cancellationToken)
    { command.RevisionId = id; return OnboardingAsync(command, OnboardingAction.Approve, cancellationToken); }

    [HttpPost("onboarding/revisions/{id:guid}/request-changes"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "update")]
    public Task<IActionResult> RequestOnboardingChanges(Guid id, [FromBody] OnboardingCommand command, CancellationToken cancellationToken)
    { command.RevisionId = id; return OnboardingAsync(command, OnboardingAction.RequestChanges, cancellationToken); }

    [HttpPost("onboarding/revisions/{id:guid}/reject"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "update")]
    public Task<IActionResult> RejectOnboarding(Guid id, [FromBody] OnboardingCommand command, CancellationToken cancellationToken)
    { command.RevisionId = id; return OnboardingAsync(command, OnboardingAction.Reject, cancellationToken); }

    [HttpPost("onboarding/documents"), AllowAnonymous, RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> UploadOnboarding([FromForm] OnboardingUploadForm form, CancellationToken cancellationToken) => UploadOnboardingAsync(form, true, cancellationToken);

    [HttpPost("me/onboarding/documents"), Authorize, RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> UploadMyOnboarding([FromForm] OnboardingUploadForm form, CancellationToken cancellationToken) => UploadOnboardingAsync(form, false, cancellationToken);

    [HttpPost("onboarding/documents/{id:guid}/read"), AllowAnonymous]
    public Task<IActionResult> ReadInvitationDocument(Guid id, [FromBody] OnboardingCommand command, CancellationToken cancellationToken) =>
        DocumentAsync(new(id, Guid.Empty, false, command.InvitationId, command.Token), cancellationToken);

    [HttpGet("onboarding/documents/{id:guid}"), Authorize]
    public Task<IActionResult> ReadOwnDocument(Guid id, CancellationToken cancellationToken) => TryGetUserId(out var actor)
        ? DocumentAsync(new(id, actor, false, null, null), cancellationToken) : Task.FromResult<IActionResult>(Unauthorized());

    [HttpGet("onboarding/review-documents/{id:guid}"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff", "read")]
    public Task<IActionResult> ReadReviewDocument(Guid id, CancellationToken cancellationToken) => TryGetUserId(out var actor)
        ? DocumentAsync(new(id, actor, true, null, null), cancellationToken) : Task.FromResult<IActionResult>(Unauthorized());

    private async Task<IActionResult> DocumentAsync(ReadStaffDocumentQuery query, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        try { var result = await _sender.Send(query, cancellationToken); return File(result.Bytes, result.ContentType); }
        catch (StaffOnboardingException exception) { return StatusCode(exception.Status, new { code = exception.Code }); }
    }

    private async Task<IActionResult> UploadOnboardingAsync(OnboardingUploadForm form, bool invitation, CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length is <= 0 or > 5 * 1024 * 1024) return BadRequest(new { code = "INVALID_DOCUMENT" });
        using var stream = new MemoryStream();
        await form.File.CopyToAsync(stream, cancellationToken);
        var command = new OnboardingCommand { ExpectedVersion = form.ExpectedVersion, Upload = new(form.Purpose, form.File.ContentType, stream.ToArray()),
            InvitationId = invitation ? form.InvitationId : null, Token = invitation ? form.Token : null };
        return await OnboardingAsync(command, OnboardingAction.Upload, cancellationToken, invitation);
    }

    private Task<IActionResult> OwnOnboardingAsync(OnboardingCommand command, OnboardingAction action, CancellationToken cancellationToken)
    { command.InvitationId = null; command.Token = null; return OnboardingAsync(command, action, cancellationToken); }

    private Task<IActionResult> OnboardingAsync(OnboardingCommand command, OnboardingAction action, CancellationToken cancellationToken, bool anonymous = false)
    {
        Response.Headers.CacheControl = "no-store";
        command.Action = action;
        command.ActorId = TryGetUserId(out var actor) ? actor : Guid.Empty;
        if (!anonymous && actor == Guid.Empty) return Task.FromResult<IActionResult>(Unauthorized());
        if (anonymous && !command.InvitationId.HasValue) return Task.FromResult<IActionResult>(BadRequest(new { code = "INVALID_INVITATION_TOKEN" }));
        return SendAsync(command, cancellationToken);
    }
}

public sealed class OnboardingUploadForm
{
    public Guid? InvitationId { get; set; }
    public string? Token { get; set; }
    public long? ExpectedVersion { get; set; }
    public string Purpose { get; set; } = "";
    public IFormFile? File { get; set; }
}

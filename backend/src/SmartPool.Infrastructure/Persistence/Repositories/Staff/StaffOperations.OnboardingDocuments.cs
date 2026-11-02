using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.StaffOnboarding;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    private async Task QueueDocumentsAsync(Guid revisionId, string reason, CancellationToken cancellationToken)
    {
        var documents = await context.StaffOnboardingDocuments.Where(item => item.RevisionId == revisionId && item.Purpose != "AVATAR" &&
            item.DeletionState != "DELETED").ToListAsync(cancellationToken);
        foreach (var document in documents)
        {
            document.DeletionState = "PENDING";
            document.DeletionReason = reason;
            document.RetryAtUtc = OnboardingNow;
        }
    }

    private static bool ValidImage(StaffDocumentUpload upload) => upload.Bytes.Length is > 0 and <= 5 * 1024 * 1024 &&
        ((upload.ContentType == "image/png" && upload.Bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) ||
         (upload.ContentType == "image/jpeg" && upload.Bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 })));

    private async Task<OnboardingWorkspace> UploadOnboardingDocumentAsync(OnboardingCommand request, CancellationToken cancellationToken)
    {
        var upload = request.Upload ?? throw new StaffOnboardingException("DOCUMENT_REQUIRED", 400);
        if (upload.Purpose is not ("FRONT" or "BACK" or "AVATAR") || !ValidImage(upload))
            throw new StaffOnboardingException("INVALID_DOCUMENT", 400);
        StaffOnboardingDocument document;
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            await LockOnboardingAsync(cancellationToken);
            var resolved = await ResolveOnboardingAsync(request, cancellationToken);
            RequireEditable(resolved.Revision, request.ExpectedVersion);
            var uploadedAt = OnboardingNow;
            document = new StaffOnboardingDocument { RevisionId = resolved.Revision!.Id, Purpose = upload.Purpose,
                PublicId = $"smartpool/staff-onboarding/{Guid.NewGuid():N}", DeliveryType = upload.Purpose == "AVATAR" ? "upload" : "authenticated",
                ContentType = upload.ContentType, Size = upload.Bytes.Length, UploaderId = resolved.EmployeeId ?? resolved.Invitation!.Id,
                UploadedAtUtc = uploadedAt, DeleteAfterUtc = upload.Purpose == "AVATAR" ? null : uploadedAt.AddDays(30), DeletionState = "UPLOADING" };
            context.StaffOnboardingDocuments.Add(document);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        var reservedPublicId = document.PublicId;
        StaffStoredAsset asset;
        try { asset = await Onboarding.Storage.UploadAsync(document, upload.Bytes, cancellationToken); }
        catch
        {
            await context.StaffOnboardingDocuments.Where(item => item.Id == document.Id).ExecuteUpdateAsync(update => update
                .SetProperty(item => item.DeletionState, "PENDING").SetProperty(item => item.DeletionReason, "UPLOAD_UNCONFIRMED"), CancellationToken.None);
            throw;
        }
        await context.StaffOnboardingDocuments.Where(item => item.Id == document.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.PublicId, reservedPublicId).SetProperty(item => item.AssetId, asset.AssetId)
            .SetProperty(item => item.AssetVersion, asset.Version).SetProperty(item => item.UploadSettledAtUtc, OnboardingNow), CancellationToken.None);
        context.ChangeTracker.Clear();
        OnboardingWorkspace workspace;
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await LockOnboardingAsync(cancellationToken);
            var resolved = await ResolveOnboardingAsync(request, cancellationToken);
            RequireEditable(resolved.Revision, request.ExpectedVersion);
            document = await context.StaffOnboardingDocuments.SingleAsync(item => item.Id == document.Id, cancellationToken);
            if (document.DeletionState != "UPLOADING") throw new StaffOnboardingException("DOCUMENT_UNAVAILABLE");
            var previous = await context.StaffOnboardingDocuments.Where(item => item.RevisionId == document.RevisionId &&
                item.Purpose == document.Purpose && item.DeletionState == "AVAILABLE").ToListAsync(cancellationToken);
            foreach (var item in previous) { item.DeletionState = "PENDING"; item.DeletionReason = "REPLACED"; item.RetryAtUtc = OnboardingNow; }
            await context.SaveChangesAsync(cancellationToken);
            document.AssetId = asset.AssetId;
            document.AssetVersion = asset.Version;
            document.DeletionState = "AVAILABLE";
            resolved.Revision!.Version++;
            await context.SaveChangesAsync(cancellationToken);
            workspace = await WorkspaceAsync("UPLOADED", resolved.Invitation, resolved.Revision, resolved.EmployeeId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            context.ChangeTracker.Clear();
            await context.StaffOnboardingDocuments.Where(item => item.Id == document.Id).ExecuteUpdateAsync(update => update
                .SetProperty(item => item.DeletionState, "PENDING").SetProperty(item => item.DeletionReason, "UPLOAD_REJECTED"), CancellationToken.None);
            throw;
        }
        await Onboarding.Cleanup.RunBatchAsync(cancellationToken);
        return workspace;
    }

    public async Task<StaffDocumentContent> ReadStaffDocumentAsync(ReadStaffDocumentQuery request, CancellationToken cancellationToken)
    {
        async Task<StaffOnboardingDocument> AuthorizeAsync()
        {
            var document = await context.StaffOnboardingDocuments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.DocumentId, cancellationToken)
                ?? throw new StaffOnboardingException("DOCUMENT_NOT_FOUND", 404);
            var revision = await context.StaffOnboardingRevisions.AsNoTracking().SingleAsync(item => item.Id == document.RevisionId, cancellationToken);
            if (document.DeletionState != "AVAILABLE" || document.DeleteAfterUtc <= OnboardingNow ||
                (document.Purpose != "AVATAR" && revision.State is "APPROVED" or "REJECTED"))
                throw new StaffOnboardingException("DOCUMENT_UNAVAILABLE", 410);
            if (request.IsAdmin) await RequireOnboardingAdminAsync(request.ActorId, cancellationToken);
            else if (request.InvitationId.HasValue)
            {
                var invitation = await ValidateInvitationTokenAsync(request.InvitationId, request.Token, cancellationToken);
                if (revision.InvitationId != invitation.Id) throw new StaffOnboardingException("DOCUMENT_FORBIDDEN", 403);
            }
            else
            {
                var owns = revision.EmployeeId == request.ActorId || await context.StaffInvitations.AnyAsync(item => item.Id == revision.InvitationId && item.CompletedUserId == request.ActorId, cancellationToken);
                if (!owns || !await context.Employees.AnyAsync(item => item.UserId == request.ActorId && item.User.IsDeleted != true, cancellationToken))
                    throw new StaffOnboardingException("DOCUMENT_FORBIDDEN", 403);
            }
            return document;
        }
        var document = await AuthorizeAsync();
        var bytes = await Onboarding.Storage.ReadAsync(document, cancellationToken);
        await AuthorizeAsync();
        return new(bytes, document.ContentType);
    }
}

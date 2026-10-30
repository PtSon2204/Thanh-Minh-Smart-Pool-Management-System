using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.CoachApplications;

public sealed class CoachApplicationOperations(
    SmartPoolDbContext context, Cloudinary cloudinary, TimeProvider timeProvider,
    ILogger<CoachApplicationOperations> logger)
    : ICoachApplicationOperations
{
    public async Task<CoachApplicationItem> SubmitAsync(Guid userId, byte[] image, string format,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.AsNoTracking()
            .Where(item => item.Id == userId && item.IsDeleted != true)
            .Select(item => new { item.IsCoach, item.Status, PrimaryRole = item.Role != null ? item.Role.Name : null })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || !string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(user.PrimaryRole, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chỉ Customer đang hoạt động được nộp chứng chỉ.");
        if (user.IsCoach) throw new InvalidOperationException("Bạn đã có quyền Coach.");
        if (await context.CoachApplications.AnyAsync(item => item.UserId == userId &&
                item.Status == "Pending", cancellationToken))
            throw new InvalidOperationException("Bạn đã có yêu cầu đang chờ duyệt.");

        using var stream = new MemoryStream(image, writable: false);
        var uploaded = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription($"certificate.{format}", stream),
            Folder = "smartpool/coach-certificates",
            PublicId = Guid.NewGuid().ToString("N"),
            Type = "authenticated",
            Overwrite = false
        });
        if (uploaded.Error is not null || string.IsNullOrWhiteSpace(uploaded.PublicId))
            throw new InvalidOperationException(uploaded.Error?.Message ?? "Không thể lưu ảnh chứng chỉ.");

        var committed = false;
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM smart_pool.users WHERE id = {userId} FOR UPDATE", cancellationToken);
            var current = await context.Users.Include(item => item.Role)
                .Include(item => item.UserProfile)
                .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
            if (current is null || current.IsDeleted == true ||
                !string.Equals(current.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(current.Role?.Name, "CUSTOMER", StringComparison.OrdinalIgnoreCase) ||
                current.IsCoach)
                throw new InvalidOperationException("Tài khoản không còn đủ điều kiện nộp yêu cầu.");
            if (await context.CoachApplications.AnyAsync(item => item.UserId == userId &&
                    item.Status == "Pending", cancellationToken))
                throw new InvalidOperationException("Bạn đã có yêu cầu đang chờ duyệt.");

            var application = new CoachApplication
            {
                Id = Guid.NewGuid(), UserId = userId,
                CertificatePublicId = uploaded.PublicId,
                CertificateFormat = format,
                Status = "Pending", SubmittedAt = timeProvider.GetUtcNow().UtcDateTime
            };
            context.CoachApplications.Add(application);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return ToItem(application, current.UserProfile?.FullName ?? current.Username);
        }
        finally
        {
            if (!committed)
            {
                try
                {
                    var deleted = await cloudinary.DestroyAsync(new DeletionParams(uploaded.PublicId)
                        { Type = "authenticated" });
                    if (deleted.Error is not null)
                        logger.LogWarning("Cannot remove orphaned coach certificate {PublicId}: {Error}",
                            uploaded.PublicId, deleted.Error.Message);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception,
                        "Cannot remove orphaned coach certificate {PublicId}", uploaded.PublicId);
                }
            }
        }
    }

    public async Task<CoachApplicationItem?> GetLatestForUserAsync(
    Guid userId,
    CancellationToken cancellationToken) =>
    await Project(
        context.CoachApplications
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.SubmittedAt)
            .ThenByDescending(item => item.Id)
    ).FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<CoachApplicationItem>> GetListForUserAsync(Guid userId,
        int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.CoachApplications.AsNoTracking()
            .Where(item => item.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await Project(
        query
            .OrderByDescending(item => item.SubmittedAt)
            .ThenByDescending(item => item.Id))
    .Skip((pageIndex - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(cancellationToken);
        return new PagedResponse<CoachApplicationItem>
        {
            Items = items, TotalCount = total, PageIndex = pageIndex, PageSize = pageSize
        };
    }

    public async Task<PagedResponse<CoachApplicationItem>> GetListAsync(string? status,
        int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.CoachApplications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(item => EF.Functions.ILike(item.Status, status.Trim()));
        var total = await query.CountAsync(cancellationToken);
        var items = await Project(
        query
            .OrderByDescending(item => item.SubmittedAt)
            .ThenByDescending(item => item.Id))
    .Skip((pageIndex - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(cancellationToken);
        return new PagedResponse<CoachApplicationItem>
        {
            Items = items, TotalCount = total, PageIndex = pageIndex, PageSize = pageSize
        };
    }

    public Task<CoachApplicationItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Project(context.CoachApplications.AsNoTracking().Where(item => item.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<CoachApplicationItem?> GetByIdForUserAsync(Guid id, Guid userId,
        CancellationToken cancellationToken) =>
        Project(context.CoachApplications.AsNoTracking()
            .Where(item => item.Id == id && item.UserId == userId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CoachCertificateAccess?> GetCertificateAsync(Guid id, Guid actorId,
        bool isAdmin, CancellationToken cancellationToken)
    {
        var application = await context.CoachApplications.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.UserId, item.CertificatePublicId, item.CertificateFormat })
            .SingleOrDefaultAsync(cancellationToken);
        if (application is null) return null;
        if (!isAdmin && application.UserId != actorId)
            throw new UnauthorizedAccessException("Không có quyền xem chứng chỉ này.");
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(5);
        var url = cloudinary.DownloadPrivate(application.CertificatePublicId,
            attachment: false, format: application.CertificateFormat,
            type: "authenticated", expiresAt: expiresAt.ToUnixTimeSeconds(),
            resourceType: "image");
        return new CoachCertificateAccess(url, expiresAt.UtcDateTime);
    }

    public async Task<CoachApplicationItem?> ReviewAsync(Guid id, Guid adminId,
        string decision, string? note, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774201001)", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.coach_applications WHERE id = {id} FOR UPDATE",
            cancellationToken);
        var application = await context.CoachApplications.Include(item => item.User)
            .ThenInclude(user => user.Role)
            .Include(item => item.User).ThenInclude(user => user.UserProfile)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (application is null) return null;
        if (application.Status != "Pending")
            throw new InvalidOperationException("Yêu cầu này đã được xử lý.");
        if (decision == "Approved")
        {
            var user = application.User;
            if (user.IsDeleted == true || !string.Equals(user.Status, "ACTIVE",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(user.Role?.Name, "CUSTOMER", StringComparison.OrdinalIgnoreCase) ||
                user.IsCoach)
                throw new InvalidOperationException("Tài khoản không còn đủ điều kiện nhận quyền Coach.");
            if (!await context.Roles.AnyAsync(item => item.Name == "COACH" &&
                    item.IsDeleted != true, cancellationToken))
                throw new InvalidOperationException("Role COACH chưa khả dụng.");
            user.IsCoach = true;
            user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        }
        application.Status = decision;
        application.ReviewedByUserId = adminId;
        application.ReviewedAt = timeProvider.GetUtcNow().UtcDateTime;
        application.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToItem(application, application.User.UserProfile?.FullName ??
            application.User.Username);
    }

    private async Task<CoachApplicationItem?> RevokeAsync(Guid id, Guid adminId,
        string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774201001)", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.coach_applications WHERE id = {id} FOR UPDATE",
            cancellationToken);
        var application = await context.CoachApplications
            .Include(item => item.User).ThenInclude(user => user.UserProfile)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (application is null) return null;
        if (application.Status != "Approved" || !application.User.IsCoach ||
            await context.CoachApplications.AnyAsync(item => item.UserId == application.UserId &&
                item.Id != id && item.Status == "Approved", cancellationToken))
            throw new InvalidOperationException("Yêu cầu này không còn là quyền Coach đang hoạt động.");

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.users WHERE id = {application.UserId} FOR UPDATE",
            cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hasOpenClasses = await context.SwimClasses.AnyAsync(item =>
            (item.CreatedByUserId == application.UserId || item.CoachUserId == application.UserId) &&
            (item.Status == "Draft" || item.Status == "Active"), cancellationToken);
        var hasFutureSessions = await context.ClassSessions.AnyAsync(item =>
            item.CoachUserId == application.UserId && item.Status == "Scheduled" &&
            item.StartsAt > now && (item.Class.Status == "Draft" ||
                item.Class.Status == "Active"), cancellationToken);
        if (hasOpenClasses || hasFutureSessions)
            throw new InvalidOperationException(
                "Cần ngừng hoặc chuyển giao các lớp và buổi học đang phụ trách trước khi thu hồi quyền Coach.");

        application.User.IsCoach = false;
        application.User.UpdatedAt = now;
        application.Status = "Revoked";
        application.RevokedByUserId = adminId;
        application.RevokedAt = now;
        application.RevokeReason = reason.Trim();
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToItem(application, application.User.UserProfile?.FullName ??
            application.User.Username);
    }

    public async Task<CoachApplicationItem?> RevokeForUserAsync(Guid userId, Guid adminId,
        string reason, CancellationToken cancellationToken)
    {
        var applicationId = await context.CoachApplications.AsNoTracking()
            .Where(item => item.UserId == userId && item.Status == "Approved")
            .OrderByDescending(item => item.SubmittedAt).ThenByDescending(item => item.Id)
            .Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
        return applicationId is null ? null : await RevokeAsync(applicationId.Value,
            adminId, reason, cancellationToken);
    }

    private static IQueryable<CoachApplicationItem> Project(IQueryable<CoachApplication> query) =>
        query.Select(item => new CoachApplicationItem(item.Id, item.UserId,
            item.User.UserProfile != null ? item.User.UserProfile.FullName : item.User.Username,
            item.Status, item.SubmittedAt, item.ReviewedByUserId, item.ReviewedAt,
            item.ReviewNote, item.RevokedByUserId, item.RevokedAt, item.RevokeReason));

    private static CoachApplicationItem ToItem(CoachApplication item, string? applicantName) =>
        new(item.Id, item.UserId, applicantName, item.Status, item.SubmittedAt,
            item.ReviewedByUserId, item.ReviewedAt, item.ReviewNote,
            item.RevokedByUserId, item.RevokedAt, item.RevokeReason);
}

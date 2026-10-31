using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Interfaces.Repositories;

public sealed record CoachApplicationItem(Guid Id, Guid UserId, string? ApplicantName,
    string Status, DateTime SubmittedAt, Guid? ReviewedByUserId, DateTime? ReviewedAt,
    string? ReviewNote, Guid? RevokedByUserId, DateTime? RevokedAt, string? RevokeReason);

public sealed record CoachCertificateAccess(string Url, DateTime ExpiresAtUtc);

public interface ICoachApplicationOperations
{
    Task<CoachApplicationItem> SubmitAsync(Guid userId, byte[] image, string format,
        CancellationToken cancellationToken);
    Task<CoachApplicationItem?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<PagedResponse<CoachApplicationItem>> GetListForUserAsync(Guid userId,
        int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task<PagedResponse<CoachApplicationItem>> GetListAsync(string? status, int pageIndex,
        int pageSize, CancellationToken cancellationToken);
    Task<CoachApplicationItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CoachApplicationItem?> GetByIdForUserAsync(Guid id, Guid userId,
        CancellationToken cancellationToken);
    Task<CoachCertificateAccess?> GetCertificateAsync(Guid id, Guid actorId, bool isAdmin,
        CancellationToken cancellationToken);
    Task<CoachApplicationItem?> ReviewAsync(Guid id, Guid adminId, string decision,
        string? note, CancellationToken cancellationToken);
    Task<CoachApplicationItem?> RevokeForUserAsync(Guid userId, Guid adminId, string reason,
        CancellationToken cancellationToken);
}

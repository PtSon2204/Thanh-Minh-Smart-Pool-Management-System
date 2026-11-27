namespace SmartPool.Application.Features.ManageCoachApplications;

public sealed record CoachApplicationItem(Guid Id, Guid UserId, string? ApplicantName,
    string Status, DateTime SubmittedAt, Guid? ReviewedByUserId, DateTime? ReviewedAt,
    string? ReviewNote, Guid? RevokedByUserId, DateTime? RevokedAt, string? RevokeReason);

public sealed record CoachCertificateAccess(string Url, DateTime ExpiresAtUtc);

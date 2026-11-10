using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed class MonitoringRepository(SmartPoolDbContext db, INotificationService notifications,
    IOptions<RiskOptions> risk, ILogger<MonitoringRepository> logger) : IMonitoringRepository
{
    private IQueryable<Warning> Visible(Guid userId, bool admin) => db.Warnings.AsNoTracking()
        .Where(x => x.SourceType == "LIVE" || admin || x.VideoJob!.UploadedBy == userId);
    private static readonly System.Linq.Expressions.Expression<Func<Warning, AlertDto>> Projection = x => new AlertDto(
        x.Id, x.SourceType, x.CamId, x.VideoJobId, x.Cam == null ? null : x.Cam.Name, x.Cam == null ? null : x.Cam.Location,
        x.TrackId, x.RiskEpisode, x.SeverityLevel.ToUpper(), x.Status.ToUpper(), x.VideoTimestamp, x.ImageUrl, x.SnapshotStatus,
        x.IncidentId, x.VerifiedBy, x.VerifiedAt, x.CreatedAt, x.Box);

    public async Task<PagedResponse<AlertDto>> ListAsync(GetAlertsQuery request, Guid userId, bool admin, CancellationToken ct)
    {
        var query = Visible(userId, admin);
        if (request.SourceType != null) query = query.Where(x => x.SourceType == request.SourceType);
        if (request.CameraId.HasValue) query = query.Where(x => x.CamId == request.CameraId);
        if (request.JobId.HasValue) query = query.Where(x => x.VideoJobId == request.JobId);
        if (request.Status != null) query = query.Where(x => x.Status.ToUpper() == request.Status);
        return new() { Items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(Projection).ToListAsync(ct),
            TotalCount = await query.CountAsync(ct), PageIndex = request.Page, PageSize = request.PageSize };
    }

    public async Task<AlertDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct) =>
        await Visible(userId, admin).Where(x => x.Id == id).Select(Projection).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();

    public async Task<AlertDto> HandleAsync(Guid id, string action, Guid userId, bool admin, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var warning = await db.Warnings.FromSqlInterpolated($"SELECT * FROM smart_pool.warnings WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException();
        if (warning.SourceType == "UPLOAD" && !admin &&
            !await db.VideoAnalysisJobs.AnyAsync(x => x.Id == warning.VideoJobId && x.UploadedBy == userId, ct)) throw new KeyNotFoundException();
        if (!string.Equals(warning.Status, "PENDING", StringComparison.OrdinalIgnoreCase)) throw new MonitoringConflictException("Cảnh báo đã được xử lý. Vui lòng tải lại.");
        warning.Status = action; warning.VerifiedBy = userId; warning.VerifiedAt = DateTime.UtcNow; warning.UpdatedAt = DateTime.UtcNow;
        if (action == "ACKNOWLEDGED" && warning.SourceType == "LIVE" && warning.IncidentId == null &&
            warning.SeverityLevel.ToUpperInvariant() is "CRITICAL" or "EMERGENCY")
        {
            var camera = await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == warning.CamId, ct);
            var incident = new Incident { Id = Guid.NewGuid(), WarningId = warning.Id, CameraId = camera.Id, Location = camera.Location,
                Title = $"Cảnh báo cứu hộ: {camera.Name}", Description = $"AI phát hiện nguy cơ tại {camera.Location}. Track {warning.TrackId}.",
                SeverityLevel = "EMERGENCY", ReportedBy = userId, IncidentTime = warning.WarningTime, Status = "Pending",
                ImageIncidents = warning.ImageUrl, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Incidents.Add(incident); warning.IncidentId = incident.Id;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var dto = await GetAsync(id, userId, admin, ct);
        if (warning.SourceType == "LIVE")
        {
            await PublishAsync("alert_updated", dto);
            if (warning.IncidentId.HasValue) await PublishAsync("incident_created", dto);
        }
        return dto;
    }

    public async Task<RiskWriteResult> RecordAsync(RecordRiskCommand command, CancellationToken ct)
    {
        if (command.SourceType is not ("LIVE" or "UPLOAD") || command.Decision.Severity is not ("WARNING" or "CRITICAL"))
            throw new ArgumentException("Invalid risk event.");
        var d = command.Decision;
        // A hash includes the source and tracker episode, never camera names or credentials.
        var root = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{command.SourceType}:{command.SourceId}:{d.TrackId}:{d.Episode}")))
            .TrimEnd('=').Replace('/', '_').Replace('+', '-');
        var prefix = root + ":";
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var lockKey = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(root)), 0);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);
        var warning = await db.Warnings.Where(x => x.SourceType == command.SourceType && x.RiskEpisode != null && x.RiskEpisode.StartsWith(prefix))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        // Handled episodes re-arm only after cooldown, with a separate auditable episode.
        if (warning != null && warning.Status != "PENDING")
        {
            var newlyCritical = warning.SeverityLevel == "WARNING" && d.Severity == "CRITICAL";
            if (!newlyCritical && (warning.Status != "CANCELLED" || command.SourceType == "UPLOAD" ||
                DateTime.UtcNow - warning.VerifiedAt < TimeSpan.FromSeconds(risk.Value.CooldownSeconds)))
                return new(await GetAsync(warning.Id, Guid.Empty, true, ct), false);
            warning = null;
        }
        var created = warning == null;
        var escalate = warning?.SeverityLevel == "WARNING" && d.Severity == "CRITICAL";
        if (created)
        {
            warning = new Warning { Id = Guid.NewGuid(), SourceType = command.SourceType, CamId = command.SourceType == "LIVE" ? command.SourceId : null,
                VideoJobId = command.SourceType == "UPLOAD" ? command.SourceId : null, TrackId = d.TrackId, RiskEpisode = prefix + Guid.NewGuid().ToString("N"),
                WarningTime = command.ObservedAt, VideoTimestamp = command.SourceType == "UPLOAD" ? d.Timestamp : null,
                SeverityLevel = d.Severity, Status = "PENDING", Confidence = (decimal)Math.Clamp(command.Confidence, 0, 1),
                Box = d.Box,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            // Mọi cảnh báo (LIVE/UPLOAD, WARNING/CRITICAL) đều chờ chụp ảnh snapshot; worker đánh dấu PENDING khi đã đủ source.
            warning.SnapshotStatus = "CAPTURING";
            db.Warnings.Add(warning);
        }
        else if (escalate) { warning!.SeverityLevel = "CRITICAL"; warning.UpdatedAt = DateTime.UtcNow; warning.WarningTime = command.ObservedAt;
            warning.Confidence = (decimal)Math.Clamp(command.Confidence, 0, 1);
            warning.Box = d.Box;
            if (command.SourceType == "UPLOAD") warning.VideoTimestamp = d.Timestamp;
            warning.SnapshotStatus = "CAPTURING"; }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var dto = await GetAsync(warning!.Id, Guid.Empty, true, ct);
        // Video tải lên là phân tích ngoại tuyến, không phát còi/popup cứu hộ hoặc lộ job cho người khác.
        if (command.SourceType == "LIVE" && (created || escalate))
            await PublishAsync(created ? "alert_created" : "alert_escalated", dto);
        return new(dto, created || escalate);
    }

    public async Task CompleteSnapshotAsync(Guid id, string url, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var warning = await db.Warnings.FromSqlInterpolated($"SELECT * FROM smart_pool.warnings WHERE id = {id} FOR UPDATE").SingleAsync(ct);
        warning.ImageUrl = url; warning.SnapshotStatus = "COMPLETED"; warning.UpdatedAt = DateTime.UtcNow;
        if (warning.IncidentId is Guid incidentId)
            await db.Incidents.Where(x => x.Id == incidentId).ExecuteUpdateAsync(set => set.SetProperty(x => x.ImageIncidents, url), ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        if (warning.SourceType == "LIVE")
            await PublishAsync("alert_updated", await GetAsync(id, Guid.Empty, true, ct));
    }

    private async Task PublishAsync(string name, AlertDto dto)
    {
        try { await notifications.SendToGroupAsync("safety-operators", name, dto); }
        catch (Exception) { logger.LogWarning("Monitoring event delivery failed: {Event} {AlertId}. Clients recover through polling.", name, dto.Id); }
    }
}

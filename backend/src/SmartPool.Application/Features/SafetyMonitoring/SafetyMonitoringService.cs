using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.SafetyMonitoring;

// Alert lifecycle and incident rules. Storage executes these changes atomically;
// transport and snapshot workers never decide the business state themselves.
public sealed class SafetyMonitoringService(IMonitoringRepository repository, INotificationService notifications,
    IOptions<RiskOptions> options, TimeProvider clock, ILogger<SafetyMonitoringService> logger) : ISafetyMonitoringService
{
    private enum WriteAction { Ignore, Create, Escalate }
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public Task<AlertDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct) => repository.GetAsync(id, userId, admin, ct);
    public Task<SmartPool.Application.Common.Models.PagedResponse<AlertDto>> ListAsync(GetAlertsQuery query, Guid userId, bool admin, CancellationToken ct) =>
        repository.ListAsync(query, userId, admin, ct);

    public async Task<AlertDto> HandleAsync(Guid id, string action, Guid userId, bool admin, CancellationToken ct, string? expectedSeverity = null)
    {
        if (action is not ("ACKNOWLEDGED" or "CANCELLED")) throw new ArgumentException("Thao tác không hợp lệ.");
        if (expectedSeverity is not (null or "WARNING" or "CRITICAL" or "EMERGENCY")) throw new ArgumentException("Mức cảnh báo không hợp lệ.");
        var result = await repository.WithAlertAsync(id, async (session, token) => {
            var warning = session.Current ?? throw new KeyNotFoundException();
            if (!string.Equals(warning.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                throw new MonitoringConflictException("Cảnh báo đã được xử lý. Vui lòng tải lại.");
            if (expectedSeverity != null && !string.Equals(warning.SeverityLevel, expectedSeverity, StringComparison.OrdinalIgnoreCase))
                throw new MonitoringConflictException("Mức cảnh báo đã thay đổi. Vui lòng kiểm tra mức mới rồi xác nhận lại.", await repository.GetAsync(id, userId, admin, token));
            if (action == "ACKNOWLEDGED" && string.IsNullOrWhiteSpace(warning.ImageUrl))
                throw new MonitoringConflictException("Cảnh báo cũ chưa có ảnh chụp. Vui lòng chờ ảnh được xử lý trước khi xác nhận.");
            var now = Now;
            warning.Status = action;
            warning.VerifiedBy = userId;
            warning.VerifiedAt = now;
            warning.UpdatedAt = now;
            var createdIncident = false;
            if (action == "ACKNOWLEDGED" && warning.IncidentId == null &&
                warning.SeverityLevel.ToUpperInvariant() is "CRITICAL" or "EMERGENCY" && warning.CamId is Guid cameraId) {
                var camera = await session.GetCameraAsync(cameraId, token);
                var incident = CreateIncident(warning, camera, userId, now);
                session.AddIncident(incident);
                warning.IncidentId = incident.Id;
                createdIncident = true;
            }
            return createdIncident;
        }, ct);
        var dto = await repository.GetAsync(id, userId, admin, ct);
        await PublishAsync("alert_updated", dto);
        if (result) await PublishAsync("incident_created", dto);
        return dto;
    }

    public async Task<RiskWriteResult> RecordAsync(RecordRiskCommand command, CancellationToken ct)
    {
        Validate(command);
        RequireImage(command.ImageUrl);
        var prefix = EpisodePrefix(command);
        var result = await repository.WithEpisodeAsync(prefix, (session, _) => {
            var current = session.Current;
            var now = Now;
            var action = Decide(current, command, now);
            if (action == WriteAction.Ignore) return Task.FromResult((Id: current!.Id, Event: (string?)null));
            Warning warning;
            if (action == WriteAction.Create) {
                warning = CreateWarning(command, current, prefix, now);
                session.AddWarning(warning);
            } else {
                warning = current!;
                EscalateWarning(warning, command, now);
            }
            return Task.FromResult((Id: warning.Id, Event: (string?)(action == WriteAction.Create ? "alert_created" : "alert_escalated")));
        }, ct);
        var dto = await repository.GetAsync(result.Id, Guid.Empty, true, ct);
        if (result.Event != null) await PublishAsync(result.Event, dto);
        return new(dto);
    }

    public async Task<Guid?> PrepareSnapshotAsync(RecordRiskCommand command, CancellationToken ct)
    {
        Validate(command);
        var current = await repository.FindEpisodeAsync(EpisodePrefix(command), ct);
        return Decide(current, command, Now) switch {
            WriteAction.Ignore => null,
            WriteAction.Escalate => current!.Id,
            _ => command.AlertId.HasValue && command.AlertId != current?.Id ? command.AlertId.Value : Guid.NewGuid(),
        };
    }

    public async Task CompleteSnapshotAsync(Guid id, string url, CancellationToken ct)
    {
        RequireImage(url);
        await repository.WithAlertAsync(id, async (session, token) => {
            var warning = session.Current ?? throw new KeyNotFoundException();
            warning.ImageUrl = url;
            warning.SnapshotStatus = "COMPLETED";
            warning.UpdatedAt = Now;
            if (warning.IncidentId is Guid incidentId) await session.UpdateIncidentImageAsync(incidentId, url, token);
            return true;
        }, ct);
        await PublishAsync("alert_updated", await repository.GetAsync(id, Guid.Empty, true, ct));
    }

    private static Incident CreateIncident(Warning warning, Equipment camera, Guid userId, DateTime now) =>
        new Incident
        {
            Id = Guid.NewGuid(),
            WarningId = warning.Id,
            CameraId = camera.Id,
            Location = camera.Location,
            Title = $"Cảnh báo cứu hộ: {camera.Name}",
            Description = $"AI phát hiện nguy cơ tại {camera.Location}. Track {warning.TrackId}.",
            SeverityLevel = "EMERGENCY",
            ReportedBy = userId,
            IncidentTime = warning.WarningTime,
            Status = "Pending",
            ImageIncidents = warning.ImageUrl,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static Warning CreateWarning(RecordRiskCommand command, Warning? current, string prefix, DateTime now)
    {
        var decision = command.Decision;
        return new Warning
        {
            Id = command.AlertId.HasValue && command.AlertId != current?.Id ? command.AlertId.Value : Guid.NewGuid(),
            CamId = command.CameraId,
            TrackId = decision.TrackId,
            RiskEpisode = prefix + Guid.NewGuid().ToString("N"),
            WarningTime = command.ObservedAt,
            SeverityLevel = decision.Severity,
            Status = "PENDING",
            Confidence = (decimal)Math.Clamp(command.Confidence, 0, 1),
            Box = decision.Box,
            ImageUrl = command.ImageUrl!,
            SnapshotStatus = "COMPLETED",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static void EscalateWarning(Warning warning, RecordRiskCommand command, DateTime now)
    {
        warning.SeverityLevel = "CRITICAL";
        warning.UpdatedAt = now;
        warning.WarningTime = command.ObservedAt;
        warning.Confidence = (decimal)Math.Clamp(command.Confidence, 0, 1);
        warning.Box = command.Decision.Box;
        warning.ImageUrl = command.ImageUrl!;
        warning.SnapshotStatus = "COMPLETED";
    }

    // Current state          Incoming observation       Result
    // Missing                WARNING/CRITICAL           Create
    // PENDING WARNING        CRITICAL                   Escalate
    // PENDING                Otherwise                  Ignore
    // Handled WARNING        CRITICAL                   Create
    // CANCELLED              After cooldown             Create
    // Handled                Otherwise                  Ignore
    private WriteAction Decide(Warning? current, RecordRiskCommand command, DateTime now)
    {
        if (current == null) return WriteAction.Create;
        var critical = current.SeverityLevel == "WARNING" && command.Decision.Severity == "CRITICAL";
        if (current.Status == "PENDING") return critical ? WriteAction.Escalate : WriteAction.Ignore;
        return critical || current.Status == "CANCELLED" &&
            now - current.VerifiedAt >= TimeSpan.FromSeconds(options.Value.Clone().CooldownSeconds)
            ? WriteAction.Create : WriteAction.Ignore;
    }

    private static void Validate(RecordRiskCommand command)
    {
        if (command.Decision.Severity is not ("WARNING" or "CRITICAL"))
            throw new ArgumentException("Invalid risk event.");
    }
    private static void RequireImage(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var image) || image.Scheme != "https" || !string.IsNullOrEmpty(image.UserInfo))
            throw new ArgumentException("Cảnh báo phải có ảnh đã upload thành công trước khi lưu.");
    }
    private static string EpisodePrefix(RecordRiskCommand command) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{command.CameraId}:{command.Decision.TrackId}:{command.Decision.Episode}")))
            .TrimEnd('=').Replace('/', '_').Replace('+', '-') + ":";
    private async Task PublishAsync(string name, AlertDto dto)
    {
        try { await notifications.SendToGroupAsync("safety-operators", name, dto); }
        catch (Exception) { logger.LogWarning("Monitoring event delivery failed: {Event} {AlertId}. Clients recover through polling.", name, dto.Id); }
    }
}

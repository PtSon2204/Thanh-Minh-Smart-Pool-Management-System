using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed class MonitoringRepository(SmartPoolDbContext db) : IMonitoringRepository
{
    private IQueryable<Warning> Visible() => db.Warnings.AsNoTracking();
    private static readonly System.Linq.Expressions.Expression<Func<Warning, AlertDto>> Projection = x => new AlertDto(
        x.Id, x.CamId, x.Cam == null ? null : x.Cam.Name, x.Cam == null ? null : x.Cam.Location,
        x.TrackId, x.RiskEpisode, x.SeverityLevel.ToUpper(), x.Status.ToUpper(), x.ImageUrl, x.SnapshotStatus,
        x.IncidentId, x.VerifiedBy, x.VerifiedAt, x.CreatedAt, x.Box)
        { ObservedAt = DateTime.SpecifyKind(x.WarningTime, DateTimeKind.Utc),
            HandledByName = x.Verifier == null ? null :
                x.Verifier.UserProfile != null && x.Verifier.UserProfile.FullName.Trim() != ""
                    ? x.Verifier.UserProfile.FullName : x.Verifier.Username };

    public async Task<AlertFilterOptions> FiltersAsync(CancellationToken ct)
    {
        var query = Visible();
        var areas = await query.Where(x => x.Cam != null && x.Cam.Location != null && x.Cam.Location != "")
            .Select(x => x.Cam!.Location!).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        var severities = await query.Select(x => x.SeverityLevel.ToUpper()).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        var statuses = await query.Select(x => x.Status.ToUpper()).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        var cameras = await query.Where(x => x.Cam != null).Select(x => new { x.Cam!.Id, x.Cam.Name })
            .Distinct().OrderBy(x => x.Name).ThenBy(x => x.Id).ToArrayAsync(ct);
        // The UI presents calendar dates in Vietnam; filter bounds are sent as UTC instants.
        var dates = await query.Select(x => x.WarningTime.AddHours(7).Date).Distinct().OrderByDescending(x => x).ToArrayAsync(ct);
        return new(areas, severities, statuses, cameras.Select(x => new AlertCameraOption(x.Id, x.Name)).ToArray(),
            dates.Select(x => x.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).ToArray());
    }

    public async Task<PagedResponse<AlertDto>> ListAsync(GetAlertsQuery request, Guid userId, bool admin, CancellationToken ct)
    {
        var query = Visible();
        if (request.CameraId.HasValue) query = query.Where(x => x.CamId == request.CameraId);
        if (request.Status != null) query = query.Where(x => x.Status.ToUpper() == request.Status);
        if (request.Area != null) query = query.Where(x => x.Cam != null && x.Cam.Location == request.Area);
        if (request.Severity != null) query = query.Where(x => x.SeverityLevel.ToUpper() == request.Severity);
        if (request.FromTime is { } from) query = query.Where(x => x.WarningTime >= from.UtcDateTime);
        if (request.ToTime is { } to) query = query.Where(x => x.WarningTime <= to.UtcDateTime);
        return new() { Items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(Projection).ToListAsync(ct),
            TotalCount = await query.CountAsync(ct), PageIndex = request.Page, PageSize = request.PageSize };
    }

    public async Task<AlertDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct) =>
        await Visible().Where(x => x.Id == id).Select(Projection).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();

    public Task<Warning?> FindEpisodeAsync(string prefix, CancellationToken ct) =>
        db.Warnings.AsNoTracking().Where(x => x.RiskEpisode != null && x.RiskEpisode.StartsWith(prefix))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);

    public async Task<T> WithAlertAsync<T>(Guid id, Func<IMonitoringWriteSession, CancellationToken, Task<T>> change, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var episode = await db.Warnings.AsNoTracking().Where(x => x.Id == id).Select(x => x.RiskEpisode).SingleOrDefaultAsync(ct);
        if (episode?.LastIndexOf(':') is int separator && separator > 0) await LockAsync(episode[..(separator + 1)], ct);
        var warning = await db.Warnings.FromSqlInterpolated($"SELECT * FROM smart_pool.warnings WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
        await db.Entry(warning).ReloadAsync(ct);
        var result = await change(new WriteSession(db, warning), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<T> WithEpisodeAsync<T>(string prefix, Func<IMonitoringWriteSession, CancellationToken, Task<T>> change, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(prefix, ct);
        var warning = await db.Warnings.Where(x => x.RiskEpisode != null && x.RiskEpisode.StartsWith(prefix))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (warning != null) await db.Entry(warning).ReloadAsync(ct);
        var result = await change(new WriteSession(db, warning), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private Task LockAsync(string prefix, CancellationToken ct)
    {
        var root = prefix.TrimEnd(':');
        var key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(root)), 0);
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }

    private sealed class WriteSession(SmartPoolDbContext db, Warning? current) : IMonitoringWriteSession
    {
        public Warning? Current => current;
        public Task<Equipment> GetCameraAsync(Guid id, CancellationToken ct) => db.Equipments.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        public void AddWarning(Warning warning) => db.Warnings.Add(warning);
        public void AddIncident(Incident incident) => db.Incidents.Add(incident);
        public Task UpdateIncidentImageAsync(Guid incidentId, string url, CancellationToken ct) =>
            db.Incidents.Where(x => x.Id == incidentId).ExecuteUpdateAsync(set => set.SetProperty(x => x.ImageIncidents, url), ct);
    }
}

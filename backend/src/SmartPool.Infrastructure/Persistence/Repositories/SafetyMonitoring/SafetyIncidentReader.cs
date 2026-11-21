using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.SafetyMonitoring;

public sealed class SafetyIncidentReader(SmartPoolDbContext db) : ISafetyIncidentReader
{
    public async Task<GetSafetyIncidentsResult> SearchAsync(GetSafetyIncidentsQuery request, CancellationToken ct)
    {
        // Summary and page must describe one snapshot while new alerts arrive.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var rows = from incident in db.Incidents.AsNoTracking()
                   join user in db.Users.AsNoTracking() on incident.ReportedBy equals (Guid?)user.Id into users
                   from user in users.DefaultIfEmpty()
                   join profile in db.UserProfiles.AsNoTracking() on incident.ReportedBy equals (Guid?)profile.UserId into profiles
                   from profile in profiles.DefaultIfEmpty()
                   select new SafetyIncidentRow { Incident = incident, User = user, Profile = profile };
        var query = rows.Select(SafetyIncidentMapper.Projection);
        if (!string.IsNullOrWhiteSpace(request.Search)) {
            var text = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Title.ToLower().Contains(text) || (x.Description != null && x.Description.ToLower().Contains(text))
                || x.ReportedByName.ToLower().Contains(text) || x.IncidentCode.ToLower().Contains(text));
        }
        if (!string.IsNullOrWhiteSpace(request.Zone) && request.Zone != "Tất cả") {
            var zone = request.Zone.ToLowerInvariant(); query = query.Where(x => x.Zone.ToLower() == zone);
        }
        if (!string.IsNullOrWhiteSpace(request.SeverityLevel) && request.SeverityLevel != "Tất cả") {
            var level = request.SeverityLevel.ToLowerInvariant(); query = query.Where(x => x.SeverityLevel.ToLower() == level);
        }
        if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != "Tất cả") {
            var status = request.Status.ToLowerInvariant(); query = query.Where(x => x.Status.ToLower() == status);
        }
        if (request.SourceType != null) {
            var camera = request.SourceType == "camera";
            query = query.Where(x => (x.SourceType == "Camera AI giám sát") == camera);
        }
        if (request.FromDate.HasValue) query = query.Where(x => x.IncidentTime >= request.FromDate.Value);
        if (request.ToDate.HasValue) query = query.Where(x => x.IncidentTime <= request.ToDate.Value);
        // Aggregate the filtered set on the server BEFORE paginating.
        var summary = await query.GroupBy(_ => 1).Select(group => new {
            Total = group.Count(), Pending = group.Count(x => x.Status == "Pending"),
            InProgress = group.Count(x => x.Status == "In Progress"), Resolved = group.Count(x => x.Status == "Resolved"),
            Emergency = group.Count(x => x.SeverityLevel.ToUpper() == "EMERGENCY"),
            Camera = group.Count(x => x.SourceType == "Camera AI giám sát"),
        }).SingleOrDefaultAsync(ct);
        var items = await query.OrderByDescending(x => x.IncidentTime).ThenBy(x => x.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new() { Items = items, TotalCount = summary?.Total ?? 0, TotalIncidents = summary?.Total ?? 0,
            PageNumber = request.PageNumber, PageSize = request.PageSize,
            PendingCount = summary?.Pending ?? 0, InProgressCount = summary?.InProgress ?? 0,
            ResolvedCount = summary?.Resolved ?? 0, EmergencyCount = summary?.Emergency ?? 0,
            CameraCount = summary?.Camera ?? 0, LifeguardCount = (summary?.Total ?? 0) - (summary?.Camera ?? 0) };
    }
}

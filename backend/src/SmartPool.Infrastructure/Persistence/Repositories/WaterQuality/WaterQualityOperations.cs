using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityAnalytics;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogs;
using SmartPool.Application.Features.ManageWaterQuality.Services;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.WaterQuality;

public sealed class WaterQualityOperations : IWaterQualityOperations
{
    private readonly SmartPoolDbContext _context;
    private readonly TimeProvider _timeProvider;

    public WaterQualityOperations(SmartPoolDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    private static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    public async Task<WaterQualityLogDto> CreateWaterQualityLogAsync(WaterQualityLog log, CancellationToken cancellationToken)
    {
        _context.WaterQualityLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        // Load relations for response DTO
        var created = await _context.WaterQualityLogs.AsNoTracking()
            .Include(l => l.Inspector)
                .ThenInclude(u => u!.UserProfile)
            .Include(l => l.Equipment)
            .FirstAsync(l => l.Id == log.Id, cancellationToken);

        return MapToDto(created);
    }

    public async Task<PagedResponse<WaterQualityLogDto>> GetWaterQualityLogsAsync(GetWaterQualityLogsQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = _context.WaterQualityLogs.AsNoTracking().AsQueryable();

        if (query.FromDate.HasValue)
        {
            var fromUtc = query.FromDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc)
                : query.FromDate.Value.ToUniversalTime();
            dbQuery = dbQuery.Where(l => l.InspectionTime >= fromUtc);
        }

        if (query.ToDate.HasValue)
        {
            var toUtc = query.ToDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc)
                : query.ToDate.Value.ToUniversalTime();
            dbQuery = dbQuery.Where(l => l.InspectionTime <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            dbQuery = dbQuery.Where(l => l.Status == query.Status);
        }

        if (query.InspectedBy.HasValue && query.InspectedBy.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(l => l.InspectedBy == query.InspectedBy.Value);
        }

        var total = await dbQuery.CountAsync(cancellationToken);
        var (page, pageSize) = Normalize(query.PageIndex, query.PageSize);

        var logs = await dbQuery
            .OrderByDescending(l => l.InspectionTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(l => l.Inspector)
                .ThenInclude(u => u!.UserProfile)
            .Include(l => l.Equipment)
            .ToListAsync(cancellationToken);

        var dtos = logs.Select(MapToDto).ToList();

        return new PagedResponse<WaterQualityLogDto>
        {
            Items = dtos,
            TotalCount = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<WaterQualityLogDto?> GetWaterQualityLogByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var log = await _context.WaterQualityLogs.AsNoTracking()
            .Include(l => l.Inspector)
                .ThenInclude(u => u!.UserProfile)
            .Include(l => l.Equipment)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        return log is null ? null : MapToDto(log);
    }

    public async Task<WaterQualityAnalyticsResponse> GetWaterQualityAnalyticsAsync(GetWaterQualityAnalyticsQuery query, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var fromUtc = query.FromDate.HasValue
            ? (query.FromDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc)
                : query.FromDate.Value.ToUniversalTime())
            : nowUtc.AddDays(-30);

        var toUtc = query.ToDate.HasValue
            ? (query.ToDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc)
                : query.ToDate.Value.ToUniversalTime())
            : nowUtc;

        var logs = await _context.WaterQualityLogs.AsNoTracking()
            .Where(l => l.InspectionTime >= fromUtc && l.InspectionTime <= toUtc)
            .OrderBy(l => l.InspectionTime)
            .Include(l => l.Inspector)
                .ThenInclude(u => u!.UserProfile)
            .Include(l => l.Equipment)
            .ToListAsync(cancellationToken);

        var response = new WaterQualityAnalyticsResponse
        {
            TotalLogs = logs.Count,
            WithinLimitsCount = logs.Count(l => l.Status == WaterQualityConstants.StatusWithinLimits),
            WarningCount = logs.Count(l => l.Status == WaterQualityConstants.StatusWarning),
            DangerCount = logs.Count(l => l.Status == WaterQualityConstants.StatusDanger)
        };

        if (response.TotalLogs > 0)
        {
            response.ComplianceRatePercentage = Math.Round((double)response.WithinLimitsCount / response.TotalLogs * 100, 1);
            response.AvgPh = Math.Round(logs.Average(l => l.PhValue), 2);
            response.MinPh = logs.Min(l => l.PhValue);
            response.MaxPh = logs.Max(l => l.PhValue);

            response.AvgChlorine = Math.Round(logs.Average(l => l.FreeChlorineMgL), 2);
            response.MinChlorine = logs.Min(l => l.FreeChlorineMgL);
            response.MaxChlorine = logs.Max(l => l.FreeChlorineMgL);

            // Group by date (yyyy-MM-dd)
            response.DailyTrends = logs
                .GroupBy(l => l.InspectionTime.ToString("yyyy-MM-dd"))
                .Select(g => new WaterQualityTrendPoint
                {
                    Date = g.Key,
                    AvgPh = Math.Round(g.Average(x => x.PhValue), 2),
                    AvgChlorine = Math.Round(g.Average(x => x.FreeChlorineMgL), 2),
                    TotalMeasurements = g.Count(),
                    WithinLimitsCount = g.Count(x => x.Status == WaterQualityConstants.StatusWithinLimits),
                    WarningCount = g.Count(x => x.Status == WaterQualityConstants.StatusWarning),
                    DangerCount = g.Count(x => x.Status == WaterQualityConstants.StatusDanger)
                })
                .OrderBy(t => t.Date)
                .ToList();

            // Recent Alerts (Warning or Danger)
            response.RecentAlerts = logs
                .Where(l => l.Status is WaterQualityConstants.StatusWarning or WaterQualityConstants.StatusDanger)
                .OrderByDescending(l => l.InspectionTime)
                .Take(10)
                .Select(MapToDto)
                .ToList();
        }

        return response;
    }

    private static WaterQualityLogDto MapToDto(WaterQualityLog log)
    {
        return new WaterQualityLogDto
        {
            Id = log.Id,
            InspectionTime = log.InspectionTime,
            PhValue = log.PhValue,
            FreeChlorineMgL = log.FreeChlorineMgL,
            IsWaterClear = log.IsWaterClear,
            Source = log.Source,
            InspectedBy = log.InspectedBy,
            InspectorName = log.Inspector?.UserProfile?.FullName ?? log.Inspector?.Username,
            EquipmentId = log.EquipmentId,
            EquipmentName = log.Equipment?.Name,
            Status = log.Status,
            Recommendation = log.Recommendation,
            EvaluationVersion = log.EvaluationVersion,
            Notes = log.Notes,
            CreatedAt = log.CreatedAt
        };
    }
}

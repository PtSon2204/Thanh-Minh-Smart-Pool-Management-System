using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;
using SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutySchedules;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityAnalytics;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogs;
using SmartPool.Application.Features.ManageWaterQuality.Services;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.WaterQuality;

public sealed partial class WaterQualityOperations : IWaterQualityOperations
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

    public Task<PoolDutySchedule?> GetOperationalTaskByIdAsync(Guid taskId, CancellationToken cancellationToken) =>
        _context.PoolDutySchedules.FirstOrDefaultAsync(item => item.Id == taskId && item.TaskType != null, cancellationToken);

    public async Task<WaterQualityLogDto> CreateWaterQualityLogAsync(WaterQualityLog log,
        CancellationToken cancellationToken, OperationalTaskExecutionAuthorization? taskExecution)
    {
        if (log.OperationalTaskId.HasValue)
        {
            if (taskExecution is null)
                throw new UnauthorizedAccessException("Cần xác thực quyền thực hiện công việc.");
            return await CreateTaskLinkedWaterQualityLogAsync(log, taskExecution, cancellationToken);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.WaterQualityLogs.Add(log);

        // Nếu nhân viên đang thực hiện đo độc lập trong ngày được giao nhiệm vụ Measurement -> tự động hoàn tất task đo ngày hôm đó
        if (log.InspectedBy.HasValue && log.InspectedBy.Value != Guid.Empty)
        {
            var logLocalDate = ToVietnamDate(log.InspectionTime);
            var openAssignedMeasurement = await _context.PoolDutySchedules
                .Where(item => item.TaskType == PoolDutyTaskTypes.Measurement
                    && item.DutyDate == logLocalDate
                    && item.AssignedEmployeeId == log.InspectedBy.Value
                    && item.Status != "Cancelled"
                    && item.Status != "Completed"
                    && !item.WaterQualityLogId.HasValue)
                .OrderBy(item => item.ScheduledAtUtc)
                .ThenBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (openAssignedMeasurement is not null)
            {
                openAssignedMeasurement.WaterQualityLogId = log.Id;
                openAssignedMeasurement.Status = "Completed";
                openAssignedMeasurement.LastExecutedBy = log.InspectedBy;
                openAssignedMeasurement.LastExecutedAtUtc = now;
                openAssignedMeasurement.LastExecutionAssigneeId = openAssignedMeasurement.AssignedEmployeeId;
                openAssignedMeasurement.LastExecutionWasAdminOverride = false;
                openAssignedMeasurement.Revision++;
                openAssignedMeasurement.UpdatedBy = log.InspectedBy;
                openAssignedMeasurement.UpdatedAt = now;
            }
        }

        var trendOutcome = await ApplyThreeDayTrendDutyIfNeededAsync(log, now, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Load relations for response DTO
        var created = await _context.WaterQualityLogs.AsNoTracking()
            .Include(l => l.Inspector)
                .ThenInclude(u => u!.UserProfile)
            .Include(l => l.Equipment)
            .FirstAsync(l => l.Id == log.Id, cancellationToken);

        var dto = MapToDto(created);
        ApplyTrendDutyToDto(dto, trendOutcome.Analysis, trendOutcome.GeneratedDuty);
        return dto;
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
        if (dtos.Count > 0)
        {
            var logIds = dtos.Select(item => item.Id).ToArray();
            var trendDutyByTrigger = await _context.PoolDutySchedules.AsNoTracking()
                .Where(item => item.TaskType == null
                    && item.TriggerWaterQualityLogId.HasValue
                    && logIds.Contains(item.TriggerWaterQualityLogId.Value))
                .GroupBy(item => item.TriggerWaterQualityLogId!.Value)
                .ToDictionaryAsync(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First(), cancellationToken);

            foreach (var dto in dtos)
            {
                if (trendDutyByTrigger.TryGetValue(dto.Id, out var linkedDuty))
                {
                    dto.ThreeDayTrendDetected = true;
                    dto.ThreeDayTrendSummary = WaterQualityTrendAnalyzer.ExtractTrendSummaryFromNotes(linkedDuty.Notes);
                    dto.TrendSuggestedChemicals = WaterQualityTrendAnalyzer.ParseSuggestedChemicalsFromNotes(linkedDuty.Notes);
                    dto.GeneratedNextDayDutyId = linkedDuty.Id;
                    dto.GeneratedNextDayDutyDate = linkedDuty.DutyDate;
                }
            }
        }

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

        if (log is null) return null;

        var dto = MapToDto(log);
        var linkedTrendDuty = await _context.PoolDutySchedules.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TaskType == null && item.TriggerWaterQualityLogId == log.Id, cancellationToken);
        var analysis = await AnalyzeThreeDayWindowEndingAtAsync(
            ToVietnamDate(log.InspectionTime),
            new WaterQualityDailyReading(ToVietnamDate(log.InspectionTime), log.PhValue, log.FreeChlorineMgL, log.IsWaterClear),
            cancellationToken);

        ApplyTrendDutyToDto(dto, analysis, linkedTrendDuty);
        return dto;
    }

    public async Task<WaterQualityTrendAnalysisResult> EvaluateThreeDayTrendPreviewAsync(
        decimal phValue, decimal freeChlorineMgL, bool? isWaterClear, CancellationToken cancellationToken)
    {
        var today = VietnamTimeBoundary.GetDate(_timeProvider);
        return await AnalyzeThreeDayWindowEndingAtAsync(
            today,
            new WaterQualityDailyReading(today, phValue, freeChlorineMgL, isWaterClear),
            cancellationToken);
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

            // Phân tích xu hướng 3 ngày gần nhất (dù chỉ số vẫn nằm trong khoảng an toàn)
            var latestLocalDate = ToVietnamDate(logs[^1].InspectionTime);
            var windowStartUtc = LocalToUtc(latestLocalDate.AddDays(-2), TimeOnly.MinValue);
            var windowEndUtc = LocalToUtc(latestLocalDate.AddDays(1), TimeOnly.MinValue);
            var windowLogs = await _context.WaterQualityLogs.AsNoTracking()
                .Where(l => l.InspectionTime >= windowStartUtc && l.InspectionTime < windowEndUtc)
                .OrderByDescending(l => l.InspectionTime)
                .ToListAsync(cancellationToken);

            var dailyLatest = windowLogs
                .GroupBy(l => ToVietnamDate(l.InspectionTime))
                .Select(g =>
                {
                    var latest = g.First();
                    return new WaterQualityDailyReading(g.Key, latest.PhValue, latest.FreeChlorineMgL, latest.IsWaterClear);
                })
                .OrderBy(r => r.Date)
                .ToList();

            var threeDayAnalysis = WaterQualityTrendAnalyzer.AnalyzeThreeDayTrend(dailyLatest);
            if (threeDayAnalysis.IsDeteriorating)
            {
                var nextDutyDate = latestLocalDate.AddDays(1);
                var nextDuty = await _context.PoolDutySchedules.AsNoTracking()
                    .Include(p => p.AssignedEmployee)
                        .ThenInclude(u => u!.UserProfile)
                    .Where(p => p.TaskType == null && p.DutyDate == nextDutyDate && p.TriggerWaterQualityLogId != null && p.Status != "Cancelled")
                    .OrderByDescending(p => p.UpdatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                response.ThreeDayTrend = new WaterQualityAnalyticsThreeDayTrendDto
                {
                    IsDeteriorating = true,
                    Summary = threeDayAnalysis.Summary,
                    Findings = threeDayAnalysis.Findings,
                    SuggestedChemicals = threeDayAnalysis.SuggestedChemicals,
                    Days = threeDayAnalysis.EvaluatedDays,
                    NextDayDutyDate = nextDutyDate,
                    NextDayDutyStatus = nextDuty?.Status,
                    NextDayAssignedEmployeeName = nextDuty?.AssignedEmployeeId.HasValue == true
                        ? (nextDuty.AssignedEmployee?.UserProfile?.FullName ?? nextDuty.AssignedEmployee?.Username)
                        : null
                };
            }
        }

        return response;
    }

    public async Task<PoolDutyOptionsResponse> GetPoolDutyOptionsAsync(CancellationToken cancellationToken)
    {
        var shifts = await _context.Shifts.AsNoTracking()
            .Where(s => s.IsActive == true)
            .OrderBy(s => s.StartTime)
            .Select(s => new PoolDutyShiftOptionDto
            {
                ShiftId = s.Id,
                Name = s.Name,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            })
            .ToListAsync(cancellationToken);

        var staffs = await _context.Users.AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.UserProfile)
            .Where(u => u.IsDeleted != true && u.Status != null && u.Status.ToUpper() == "ACTIVE"
                && u.Role != null && u.Role.Name.ToUpper() == "LIFEGUARD")
            .OrderBy(u => u.Username)
            .Select(u => new PoolDutyStaffOptionDto
            {
                EmployeeId = u.Id,
                FullName = u.UserProfile != null && !string.IsNullOrEmpty(u.UserProfile.FullName)
                    ? u.UserProfile.FullName
                    : (u.Username ?? "Nhân viên cứu hộ"),
                Email = u.Email,
                RoleName = u.Role != null ? u.Role.Name : "LIFEGUARD"
            })
            .ToListAsync(cancellationToken);

        return new PoolDutyOptionsResponse
        {
            Shifts = shifts,
            Staffs = staffs
        };
    }

    public async Task<PagedResponse<PoolDutyScheduleDto>> GetPoolDutySchedulesAsync(GetPoolDutySchedulesQuery query, CancellationToken cancellationToken)
    {
        var (page, pageSize) = Normalize(query.PageIndex, query.PageSize);

        // Kết nối trực tiếp tới công việc Đo chất lượng nước (TaskType == Measurement) do Admin phân công trong Công việc vận hành
        var measurementTaskQuery = _context.PoolDutySchedules.AsNoTracking()
            .Include(p => p.Shift)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.UserProfile)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.Role)
            .Include(p => p.WaterQualityLog)
                .ThenInclude(l => l!.Inspector)
                    .ThenInclude(u => u!.UserProfile)
            .Where(p => p.TaskType == PoolDutyTaskTypes.Measurement && p.Status != "Cancelled");

        if (query.FromDate.HasValue)
        {
            measurementTaskQuery = measurementTaskQuery.Where(p => p.DutyDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            measurementTaskQuery = measurementTaskQuery.Where(p => p.DutyDate <= query.ToDate.Value);
        }

        if (query.ShiftId.HasValue && query.ShiftId.Value != Guid.Empty)
        {
            measurementTaskQuery = measurementTaskQuery.Where(p => p.ShiftId == query.ShiftId.Value);
        }

        if (query.EmployeeId.HasValue && query.EmployeeId.Value != Guid.Empty)
        {
            measurementTaskQuery = measurementTaskQuery.Where(p => p.AssignedEmployeeId == query.EmployeeId.Value);
        }

        var measurementTasks = await measurementTaskQuery
            .OrderBy(p => p.DutyDate)
            .ThenByDescending(p => p.WaterQualityLogId.HasValue)
            .ThenByDescending(p => p.AssignedEmployeeId.HasValue)
            .ThenByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        // Mỗi ngày tối đa 1 công việc đo nước đại diện (phòng dữ liệu cũ còn nhiều recurrence)
        var primaryMeasurementTasks = measurementTasks
            .GroupBy(p => p.DutyDate)
            .Select(g => g.First())
            .OrderBy(p => p.DutyDate)
            .ToList();

        // Lấy thêm Lịch trực bể phát sinh (TaskType == null && TriggerWaterQualityLogId != null, tự sinh từ xu hướng 3 ngày đi xuống)
        var trendDutyQuery = _context.PoolDutySchedules.AsNoTracking()
            .Include(p => p.Shift)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.UserProfile)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.Role)
            .Include(p => p.WaterQualityLog)
            .Where(p => p.TaskType == null && p.TriggerWaterQualityLogId != null && p.Status != "Cancelled");

        if (query.FromDate.HasValue)
        {
            trendDutyQuery = trendDutyQuery.Where(p => p.DutyDate >= query.FromDate.Value);
        }
        if (query.ToDate.HasValue)
        {
            trendDutyQuery = trendDutyQuery.Where(p => p.DutyDate <= query.ToDate.Value);
        }
        if (query.ShiftId.HasValue && query.ShiftId.Value != Guid.Empty)
        {
            trendDutyQuery = trendDutyQuery.Where(p => p.ShiftId == query.ShiftId.Value);
        }
        if (query.EmployeeId.HasValue && query.EmployeeId.Value != Guid.Empty)
        {
            trendDutyQuery = trendDutyQuery.Where(p => p.AssignedEmployeeId == query.EmployeeId.Value);
        }

        var trendDuties = await trendDutyQuery
            .OrderBy(p => p.DutyDate)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        if (primaryMeasurementTasks.Count == 0 && trendDuties.Count == 0 && (!query.FromDate.HasValue || !query.ToDate.HasValue))
        {
            return new PagedResponse<PoolDutyScheduleDto>
            {
                Items = [],
                TotalCount = 0,
                PageIndex = page,
                PageSize = pageSize
            };
        }

        var minDate = query.FromDate ?? new[] { primaryMeasurementTasks.FirstOrDefault()?.DutyDate ?? DateOnly.MaxValue, trendDuties.FirstOrDefault()?.DutyDate ?? DateOnly.MaxValue }.Min();
        var maxDate = query.ToDate ?? new[] { primaryMeasurementTasks.LastOrDefault()?.DutyDate ?? DateOnly.MinValue, trendDuties.LastOrDefault()?.DutyDate ?? DateOnly.MinValue }.Max();
        if (minDate > maxDate)
        {
            (minDate, maxDate) = (maxDate, minDate);
        }

        var fromUtc = LocalToUtc(minDate, TimeOnly.MinValue);
        var toUtcExclusive = LocalToUtc(maxDate.AddDays(1), TimeOnly.MinValue);

        // Mỗi ngày chỉ cần 1 lần đo chất lượng nước — lấy phiếu đo mới nhất của từng ngày (theo giờ Việt Nam)
        var logsInRange = await _context.WaterQualityLogs.AsNoTracking()
            .Include(l => l.Inspector)
                .ThenInclude(u => u!.UserProfile)
            .Where(l => l.InspectionTime >= fromUtc && l.InspectionTime < toUtcExclusive)
            .OrderByDescending(l => l.InspectionTime)
            .ToListAsync(cancellationToken);

        var dailyLogs = logsInRange
            .GroupBy(l => ToVietnamDate(l.InspectionTime))
            .ToDictionary(g => g.Key, g => g.First());

        var empShiftIds = primaryMeasurementTasks
            .Where(p => p.EmployeeShiftId.HasValue)
            .Select(p => p.EmployeeShiftId!.Value)
            .Distinct()
            .ToList();
        var presentEmpShiftIds = empShiftIds.Count > 0
            ? (await _context.EmployeeShifts.AsNoTracking()
                .Where(es => empShiftIds.Contains(es.Id) && es.Status != null && es.Status.ToUpper() == "PRESENT")
                .Select(es => es.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : [];

        var mappedShifts = primaryMeasurementTasks.Select(task =>
        {
            dailyLogs.TryGetValue(task.DutyDate, out var dayLog);
            var resolvedLog = task.WaterQualityLog ?? dayLog;
            var effectiveStatus = resolvedLog is not null || string.Equals(task.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                ? "Completed"
                : !task.AssignedEmployeeId.HasValue || string.Equals(task.Status, "Unassigned", StringComparison.OrdinalIgnoreCase)
                    ? "Unassigned"
                    : task.EmployeeShiftId.HasValue && presentEmpShiftIds.Contains(task.EmployeeShiftId.Value)
                        ? "InProgress"
                        : "Scheduled";

            var employeeName = task.AssignedEmployee is not null
                ? (!string.IsNullOrWhiteSpace(task.AssignedEmployee.UserProfile?.FullName)
                    ? task.AssignedEmployee.UserProfile.FullName
                    : task.AssignedEmployee.Username ?? "Nhân viên cứu hộ")
                : (resolvedLog?.Inspector?.UserProfile?.FullName ?? resolvedLog?.Inspector?.Username ?? "Chờ Admin phân công");

            var inspectorName = resolvedLog?.Inspector?.UserProfile?.FullName ?? resolvedLog?.Inspector?.Username;

            return new PoolDutyScheduleDto
            {
                Id = task.Id,
                DutyDate = task.DutyDate,
                ShiftId = task.ShiftId,
                ShiftName = task.Shift?.Name ?? "Ca đo trong ngày",
                ShiftStartTime = task.Shift?.StartTime ?? TimeOnly.MinValue,
                ShiftEndTime = task.Shift?.EndTime ?? TimeOnly.MinValue,
                TaskType = task.TaskType,
                EmployeeShiftId = task.EmployeeShiftId,
                ScheduledAtUtc = task.ScheduledAtUtc,
                AssignedEmployeeId = task.AssignedEmployeeId,
                AssignedEmployeeName = employeeName,
                AssignedEmployeeEmail = task.AssignedEmployee?.Email,
                EmployeeRole = task.AssignedEmployee?.Role?.Name ?? "LIFEGUARD",
                Status = effectiveStatus,
                AssignmentType = task.AssignmentType,
                MeasurementRecurrenceId = task.MeasurementRecurrenceId,
                Revision = task.Revision,
                WaterQualityLogId = resolvedLog?.Id,
                MeasuredPh = resolvedLog?.PhValue,
                MeasuredChlorine = resolvedLog?.FreeChlorineMgL,
                MeasurementStatus = resolvedLog?.Status,
                MeasuredAt = resolvedLog?.InspectionTime,
                Notes = resolvedLog is not null
                    ? $"Đã đo 1/1 lần trong ngày{(string.IsNullOrWhiteSpace(inspectorName) ? "" : $" ({inspectorName})")}"
                    : "Đồng bộ từ Công việc vận hành (Đo chất lượng nước 1 lần/ngày)",
                IsTrendDuty = false,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
        }).ToList();

        if (!query.ShiftId.HasValue && dailyLogs.Count > 0)
        {
            var existingMeasurementDates = (await _context.PoolDutySchedules.AsNoTracking()
                .Where(p => p.TaskType == PoolDutyTaskTypes.Measurement && p.Status != "Cancelled" && p.DutyDate >= minDate && p.DutyDate <= maxDate)
                .Select(p => p.DutyDate)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            foreach (var (logDate, dayLog) in dailyLogs)
            {
                if (existingMeasurementDates.Contains(logDate)) continue;
                if (query.EmployeeId.HasValue && query.EmployeeId.Value != Guid.Empty && dayLog.InspectedBy != query.EmployeeId.Value) continue;

                var inspectorName = dayLog.Inspector?.UserProfile?.FullName ?? dayLog.Inspector?.Username ?? "Nhân viên cứu hộ";
                mappedShifts.Add(new PoolDutyScheduleDto
                {
                    Id = dayLog.Id,
                    DutyDate = logDate,
                    ShiftName = "Đo hằng ngày",
                    AssignedEmployeeId = dayLog.InspectedBy,
                    AssignedEmployeeName = inspectorName,
                    EmployeeRole = "LIFEGUARD",
                    Status = "Completed",
                    AssignmentType = "Manual",
                    WaterQualityLogId = dayLog.Id,
                    MeasuredPh = dayLog.PhValue,
                    MeasuredChlorine = dayLog.FreeChlorineMgL,
                    MeasurementStatus = dayLog.Status,
                    MeasuredAt = dayLog.InspectionTime,
                    Notes = $"Đã đo 1/1 lần trong ngày ({inspectorName})",
                    IsTrendDuty = false,
                    CreatedAt = dayLog.CreatedAt,
                    UpdatedAt = dayLog.CreatedAt
                });
            }
        }

        var mappedTrendDuties = trendDuties.Select(p =>
        {
            var dto = MapToPoolDutyDto(p);
            dto.IsTrendDuty = true;
            dto.TrendSummary = WaterQualityTrendAnalyzer.ExtractTrendSummaryFromNotes(p.Notes);
            dto.SuggestedChemicals = WaterQualityTrendAnalyzer.ParseSuggestedChemicalsFromNotes(p.Notes);
            if (!p.AssignedEmployeeId.HasValue)
            {
                dto.AssignedEmployeeName = "Chờ Admin phân công";
            }
            if (!dto.WaterQualityLogId.HasValue && dailyLogs.TryGetValue(p.DutyDate, out var dateLog))
            {
                dto.MeasuredPh ??= dateLog.PhValue;
                dto.MeasuredChlorine ??= dateLog.FreeChlorineMgL;
                dto.MeasurementStatus ??= dateLog.Status;
                dto.MeasuredAt ??= dateLog.InspectionTime;
            }
            return dto;
        });

        var mapped = mappedShifts.Concat(mappedTrendDuties);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            mapped = mapped.Where(item => string.Equals(item.Status, query.Status, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = mapped
            .OrderBy(item => item.DutyDate)
            .ThenBy(item => item.IsTrendDuty ? 1 : 0)
            .ThenBy(item => item.ShiftStartTime)
            .ToList();
        var pagedItems = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResponse<PoolDutyScheduleDto>
        {
            Items = pagedItems,
            TotalCount = filtered.Count,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<TodayWaterQualityGateDto> GetTodayWaterQualityGateAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var today = VietnamTimeBoundary.GetDate(_timeProvider);

        // Chỉ khóa đối với nhân viên được giao công việc đo nước (Measurement) ngày hôm nay
        var hasAssignedMeasurementToday = await _context.PoolDutySchedules.AsNoTracking()
            .AnyAsync(p => p.TaskType == PoolDutyTaskTypes.Measurement
                && p.DutyDate == today
                && p.AssignedEmployeeId == employeeId
                && p.Status != "Cancelled"
                && p.Status != "Completed"
                && !p.WaterQualityLogId.HasValue,
                cancellationToken);

        if (!hasAssignedMeasurementToday)
        {
            return new TodayWaterQualityGateDto(false, today, null, null);
        }

        return new TodayWaterQualityGateDto(
            true,
            today,
            null,
            $"Bạn được giao nhiệm vụ đo chất lượng nước ngày {today:dd/MM/yyyy}. Vui lòng hoàn thành đo chất lượng nước trước khi thực hiện các công việc khác.");
    }

    public async Task<PoolDutyScheduleDto> AssignTrendPoolDutyScheduleAsync(
        Guid scheduleId, Guid assignedEmployeeId, Guid? shiftId, Guid actorId, CancellationToken cancellationToken)
    {
        var schedule = await _context.PoolDutySchedules
            .FirstOrDefaultAsync(p => p.Id == scheduleId && p.TaskType == null, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lịch trực bể cần phân công.");

        if (schedule.Status is "Cancelled" or "Completed")
        {
            throw new InvalidOperationException("Không thể phân công lịch trực bể đã kết thúc.");
        }

        var lifeguard = await _context.Employees.AsNoTracking()
            .Include(e => e.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(e => e.UserId == assignedEmployeeId
                && e.Status == "Working"
                && e.User.IsDeleted != true
                && e.User.Status != null
                && e.User.Status.ToUpper() == "ACTIVE"
                && e.User.Role != null
                && e.User.Role.Name.ToUpper() == "LIFEGUARD",
                cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên được chọn phải là nhân viên cứu hộ đang hoạt động.");

        Guid? resolvedShiftId = shiftId;
        Guid? resolvedEmployeeShiftId = null;

        var empShift = await _context.EmployeeShifts.AsNoTracking()
            .Where(es => es.EmployeeId == assignedEmployeeId
                && es.WorkDate == schedule.DutyDate
                && es.Status != "Cancelled"
                && (!resolvedShiftId.HasValue || es.ShiftId == resolvedShiftId.Value))
            .OrderBy(es => es.Shift.StartTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (empShift is not null)
        {
            resolvedShiftId = empShift.ShiftId;
            resolvedEmployeeShiftId = empShift.Id;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        schedule.AssignedEmployeeId = lifeguard.UserId;
        schedule.PreferredEmployeeId = lifeguard.UserId;
        schedule.ShiftId = resolvedShiftId;
        schedule.EmployeeShiftId = resolvedEmployeeShiftId;
        schedule.Status = "Scheduled";
        schedule.AssignmentType = "Manual";
        schedule.Revision++;
        schedule.UpdatedBy = actorId != Guid.Empty ? actorId : null;
        schedule.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        var saved = await _context.PoolDutySchedules.AsNoTracking()
            .Include(p => p.Shift)
            .Include(p => p.AssignedEmployee).ThenInclude(u => u!.UserProfile)
            .Include(p => p.AssignedEmployee).ThenInclude(u => u!.Role)
            .Include(p => p.WaterQualityLog)
            .FirstAsync(p => p.Id == schedule.Id, cancellationToken);

        var dto = MapToPoolDutyDto(saved);
        dto.IsTrendDuty = true;
        dto.TrendSummary = WaterQualityTrendAnalyzer.ExtractTrendSummaryFromNotes(saved.Notes);
        dto.SuggestedChemicals = WaterQualityTrendAnalyzer.ParseSuggestedChemicalsFromNotes(saved.Notes);
        return dto;
    }

    public async Task<PoolDutyScheduleDto> CompleteTrendPoolDutyScheduleAsync(
        Guid scheduleId, Guid actorId, Guid? waterQualityLogId, CancellationToken cancellationToken)
    {
        var schedule = await _context.PoolDutySchedules
            .FirstOrDefaultAsync(p => p.Id == scheduleId && p.TaskType == null, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lịch trực bể cần hoàn thành.");

        if (schedule.Status == "Cancelled")
        {
            throw new InvalidOperationException("Lịch trực bể đã bị hủy.");
        }

        if (schedule.Status == "Completed")
        {
            throw new InvalidOperationException("Lịch trực bể này đã được hoàn thành trước đó.");
        }

        if (!schedule.AssignedEmployeeId.HasValue || string.Equals(schedule.Status, "Unassigned", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Lịch trực bể chưa được Admin phân công nhân viên phụ trách.");
        }

        if (actorId == Guid.Empty || schedule.AssignedEmployeeId.Value != actorId)
        {
            throw new UnauthorizedAccessException("Chỉ nhân viên được phân công mới có thể xác nhận hoàn thành lịch trực bể này.");
        }

        Guid? resolvedLogId = waterQualityLogId;
        if (!resolvedLogId.HasValue)
        {
            var fromUtc = LocalToUtc(schedule.DutyDate, TimeOnly.MinValue);
            var toUtcExclusive = LocalToUtc(schedule.DutyDate.AddDays(1), TimeOnly.MinValue);
            resolvedLogId = await _context.WaterQualityLogs.AsNoTracking()
                .Where(l => l.InspectionTime >= fromUtc && l.InspectionTime < toUtcExclusive)
                .OrderByDescending(l => l.InspectionTime)
                .Select(l => (Guid?)l.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        schedule.Status = "Completed";
        schedule.WaterQualityLogId = resolvedLogId ?? schedule.WaterQualityLogId;
        schedule.LastExecutedBy = actorId != Guid.Empty ? actorId : schedule.LastExecutedBy;
        schedule.LastExecutedAtUtc = now;
        schedule.Revision++;
        schedule.UpdatedBy = actorId != Guid.Empty ? actorId : null;
        schedule.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        var saved = await _context.PoolDutySchedules.AsNoTracking()
            .Include(p => p.Shift)
            .Include(p => p.AssignedEmployee).ThenInclude(u => u!.UserProfile)
            .Include(p => p.AssignedEmployee).ThenInclude(u => u!.Role)
            .Include(p => p.WaterQualityLog)
            .FirstAsync(p => p.Id == schedule.Id, cancellationToken);

        var dto = MapToPoolDutyDto(saved);
        dto.IsTrendDuty = true;
        dto.TrendSummary = WaterQualityTrendAnalyzer.ExtractTrendSummaryFromNotes(saved.Notes);
        dto.SuggestedChemicals = WaterQualityTrendAnalyzer.ParseSuggestedChemicalsFromNotes(saved.Notes);
        return dto;
    }

    private async Task<(WaterQualityTrendAnalysisResult Analysis, PoolDutySchedule? GeneratedDuty)> ApplyThreeDayTrendDutyIfNeededAsync(
        WaterQualityLog currentLog, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var logDate = ToVietnamDate(currentLog.InspectionTime);
        var currentReading = new WaterQualityDailyReading(logDate, currentLog.PhValue, currentLog.FreeChlorineMgL, currentLog.IsWaterClear);
        var analysis = await AnalyzeThreeDayWindowEndingAtAsync(logDate, currentReading, cancellationToken);
        if (!analysis.IsDeteriorating)
        {
            return (analysis, null);
        }

        var nextDutyDate = logDate.AddDays(1);
        var existing = await _context.PoolDutySchedules
            .Where(p => p.TaskType == null
                && p.DutyDate == nextDutyDate
                && p.TriggerWaterQualityLogId != null
                && p.Status != "Cancelled"
                && p.Status != "Completed")
            .OrderByDescending(p => p.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            existing.TriggerWaterQualityLogId = currentLog.Id;
            existing.Notes = analysis.DutyNotes;
            existing.Revision++;
            existing.UpdatedBy = currentLog.InspectedBy;
            existing.UpdatedAt = nowUtc;
            return (analysis, existing);
        }

        var nextDuty = new PoolDutySchedule
        {
            Id = Guid.NewGuid(),
            TaskType = null,
            DutyDate = nextDutyDate,
            ShiftId = null,
            AssignedEmployeeId = null,
            PreferredEmployeeId = null,
            TriggerWaterQualityLogId = currentLog.Id,
            Status = "Unassigned",
            AssignmentType = "Auto",
            UnassignedReason = "ADMIN_ASSIGNMENT_REQUIRED",
            Notes = analysis.DutyNotes,
            Revision = 1,
            CreatedBy = currentLog.InspectedBy,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
        _context.PoolDutySchedules.Add(nextDuty);
        return (analysis, nextDuty);
    }

    private async Task<WaterQualityTrendAnalysisResult> AnalyzeThreeDayWindowEndingAtAsync(
        DateOnly targetDate, WaterQualityDailyReading currentReading, CancellationToken cancellationToken)
    {
        var twoDaysAgo = targetDate.AddDays(-2);
        var fromUtc = LocalToUtc(twoDaysAgo, TimeOnly.MinValue);
        var toUtcExclusive = LocalToUtc(targetDate, TimeOnly.MinValue);

        var previousLogs = await _context.WaterQualityLogs.AsNoTracking()
            .Where(l => l.InspectionTime >= fromUtc && l.InspectionTime < toUtcExclusive)
            .OrderByDescending(l => l.InspectionTime)
            .ToListAsync(cancellationToken);

        var readingsByDate = previousLogs
            .GroupBy(l => ToVietnamDate(l.InspectionTime))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latest = g.First();
                    return new WaterQualityDailyReading(g.Key, latest.PhValue, latest.FreeChlorineMgL, latest.IsWaterClear);
                });

        if (!readingsByDate.TryGetValue(twoDaysAgo, out var day0) ||
            !readingsByDate.TryGetValue(targetDate.AddDays(-1), out var day1))
        {
            return new WaterQualityTrendAnalysisResult();
        }

        return WaterQualityTrendAnalyzer.AnalyzeThreeDayTrend([day0, day1, currentReading]);
    }

    private static DateOnly ToVietnamDate(DateTime utcTime) =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), VietnamTimeZone));

    private static void ApplyTrendDutyToDto(
        WaterQualityLogDto dto, WaterQualityTrendAnalysisResult analysis, PoolDutySchedule? generatedDuty)
    {
        if (!analysis.IsDeteriorating && generatedDuty is null)
        {
            return;
        }

        dto.ThreeDayTrendDetected = true;
        dto.ThreeDayTrendSummary = !string.IsNullOrWhiteSpace(analysis.Summary)
            ? analysis.Summary
            : WaterQualityTrendAnalyzer.ExtractTrendSummaryFromNotes(generatedDuty?.Notes);
        dto.TrendSuggestedChemicals = analysis.SuggestedChemicals.Count > 0
            ? analysis.SuggestedChemicals
            : WaterQualityTrendAnalyzer.ParseSuggestedChemicalsFromNotes(generatedDuty?.Notes);
        dto.GeneratedNextDayDutyId = generatedDuty?.Id;
        dto.GeneratedNextDayDutyDate = generatedDuty?.DutyDate;
    }

    public async Task<AutoAssignPoolDutyResultDto> AutoAssignPoolDutySchedulesAsync(AutoAssignPoolDutyCommand command, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var shiftsQuery = _context.Shifts.AsNoTracking().AsQueryable();
        if (command.ShiftIds != null && command.ShiftIds.Count > 0)
        {
            shiftsQuery = shiftsQuery.Where(s => command.ShiftIds.Contains(s.Id));
        }
        var shifts = await shiftsQuery.OrderBy(s => s.StartTime).ToListAsync(cancellationToken);
        if (shifts.Count == 0)
        {
            return new AutoAssignPoolDutyResultDto
            {
                TotalSlotsProcessed = 0,
                Message = "Không có ca trực nào được chọn."
            };
        }

        List<Guid> candidateIds;
        if (command.CandidateEmployeeIds != null && command.CandidateEmployeeIds.Count > 0)
        {
            candidateIds = command.CandidateEmployeeIds;
        }
        else
        {
            var lifeguardCandidates = await _context.Users.AsNoTracking()
                .Where(u => u.IsDeleted != true && u.Role != null && u.Role.Name.ToUpper() == "LIFEGUARD")
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            candidateIds = lifeguardCandidates.Count > 0
                ? lifeguardCandidates
                : await _context.Users.AsNoTracking()
                    .Where(u => u.IsDeleted != true && u.Role != null && u.Role.Name.ToUpper() != "CUSTOMER")
                    .Select(u => u.Id)
                    .ToListAsync(cancellationToken);
        }

        if (candidateIds.Count == 0)
        {
            return new AutoAssignPoolDutyResultDto
            {
                TotalSlotsProcessed = 0,
                Message = "Không có nhân viên nào phù hợp để phân công."
            };
        }

        var existingSchedules = await _context.PoolDutySchedules
            .Where(p => p.TaskType == null && p.DutyDate >= command.FromDate && p.DutyDate <= command.ToDate)
            .ToListAsync(cancellationToken);

        var assignedList = new List<PoolDutySchedule>();
        int createdCount = 0;
        int updatedCount = 0;
        int skippedCount = 0;
        int totalSlots = 0;
        int candidateIndex = 0;

        for (var date = command.FromDate; date <= command.ToDate; date = date.AddDays(1))
        {
            foreach (var shift in shifts)
            {
                totalSlots++;
                var existing = existingSchedules.FirstOrDefault(e => e.TaskType == null && e.DutyDate == date && e.ShiftId == shift.Id);

                if (existing != null)
                {
                    if (!command.OverwriteExisting || existing.Status == "Completed")
                    {
                        skippedCount++;
                        continue;
                    }

                    var assignedEmpId = candidateIds[candidateIndex % candidateIds.Count];
                    candidateIndex++;

                    existing.AssignedEmployeeId = assignedEmpId;
                    existing.AssignmentType = "Auto";
                    existing.Status = "Scheduled";
                    if (!string.IsNullOrWhiteSpace(command.DefaultNotes))
                    {
                        existing.Notes = command.DefaultNotes;
                    }
                    existing.UpdatedAt = now;
                    updatedCount++;
                    assignedList.Add(existing);
                }
                else
                {
                    var assignedEmpId = candidateIds[candidateIndex % candidateIds.Count];
                    candidateIndex++;

                    var newSchedule = new PoolDutySchedule
                    {
                        Id = Guid.NewGuid(),
                        TaskType = null,
                        DutyDate = date,
                        ShiftId = shift.Id,
                        AssignedEmployeeId = assignedEmpId,
                        Status = "Scheduled",
                        AssignmentType = "Auto",
                        Notes = command.DefaultNotes,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _context.PoolDutySchedules.Add(newSchedule);
                    createdCount++;
                    assignedList.Add(newSchedule);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var assignedIds = assignedList.Select(a => a.Id).ToList();
        var resultDtos = await _context.PoolDutySchedules.AsNoTracking()
            .Include(p => p.Shift)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.UserProfile)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.Role)
            .Where(p => p.TaskType == null && assignedIds.Contains(p.Id))
            .OrderBy(p => p.DutyDate)
            .ThenBy(p => p.Shift != null ? p.Shift.StartTime : TimeOnly.MinValue)
            .Select(p => MapToPoolDutyDto(p))
            .ToListAsync(cancellationToken);

        return new AutoAssignPoolDutyResultDto
        {
            TotalSlotsProcessed = totalSlots,
            CreatedCount = createdCount,
            UpdatedCount = updatedCount,
            SkippedCount = skippedCount,
            Message = $"Đã tự động phân công {createdCount} ca mới, cập nhật {updatedCount} ca, bỏ qua {skippedCount} ca.",
            AssignedSchedules = resultDtos
        };
    }

    public async Task<PoolDutyScheduleDto> SavePoolDutyScheduleAsync(UpdatePoolDutyScheduleCommand command, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        PoolDutySchedule schedule;

        if (command.Id.HasValue && command.Id.Value != Guid.Empty)
        {
            schedule = await _context.PoolDutySchedules
                .FirstOrDefaultAsync(p => p.TaskType == null && p.Id == command.Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy lịch trực có mã {command.Id.Value}.");

            schedule.ShiftId = command.ShiftId;
            schedule.AssignedEmployeeId = command.AssignedEmployeeId;
            schedule.Status = command.Status;
            schedule.AssignmentType = "Manual";
            schedule.Notes = command.Notes;
            schedule.WaterQualityLogId = command.WaterQualityLogId;
            schedule.UpdatedAt = now;
        }
        else
        {
            var existing = await _context.PoolDutySchedules
                .FirstOrDefaultAsync(p => p.TaskType == null && p.DutyDate == command.DutyDate && p.ShiftId == command.ShiftId, cancellationToken);

            if (existing != null)
            {
                schedule = existing;
                schedule.AssignedEmployeeId = command.AssignedEmployeeId;
                schedule.Status = command.Status;
                schedule.AssignmentType = "Manual";
                schedule.Notes = command.Notes;
                schedule.WaterQualityLogId = command.WaterQualityLogId;
                schedule.UpdatedAt = now;
            }
            else
            {
                schedule = new PoolDutySchedule
                {
                    Id = Guid.NewGuid(),
                    TaskType = null,
                    DutyDate = command.DutyDate,
                    ShiftId = command.ShiftId,
                    AssignedEmployeeId = command.AssignedEmployeeId,
                    Status = command.Status,
                    AssignmentType = "Manual",
                    Notes = command.Notes,
                    WaterQualityLogId = command.WaterQualityLogId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.PoolDutySchedules.Add(schedule);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var saved = await _context.PoolDutySchedules.AsNoTracking()
            .Include(p => p.Shift)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.UserProfile)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.Role)
            .Include(p => p.WaterQualityLog)
            .FirstAsync(p => p.TaskType == null && p.Id == schedule.Id, cancellationToken);

        return MapToPoolDutyDto(saved);
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
            OperationalTaskId = log.OperationalTaskId,
            Status = log.Status,
            Recommendation = log.Recommendation,
            EvaluationVersion = log.EvaluationVersion,
            Notes = log.Notes,
            CreatedAt = log.CreatedAt
        };
    }

    private static PoolDutyScheduleDto MapToPoolDutyDto(PoolDutySchedule p)
    {
        return new PoolDutyScheduleDto
        {
            Id = p.Id,
            DutyDate = p.DutyDate,
            ShiftId = p.ShiftId,
            ShiftName = p.Shift?.Name ?? "Ca trực",
            ShiftStartTime = p.Shift?.StartTime ?? TimeOnly.MinValue,
            ShiftEndTime = p.Shift?.EndTime ?? TimeOnly.MinValue,
            TaskType = p.TaskType,
            InspectionWindow = p.InspectionWindow,
            EmployeeShiftId = p.EmployeeShiftId,
            ScheduledAtUtc = p.ScheduledAtUtc,
            DueAtUtc = p.DueAtUtc,
            AssignedEmployeeId = p.AssignedEmployeeId,
            PreferredEmployeeId = p.PreferredEmployeeId,
            AssignedEmployeeName = p.AssignedEmployee?.UserProfile?.FullName
                ?? p.AssignedEmployee?.Username
                ?? "Chưa xác định",
            AssignedEmployeeEmail = p.AssignedEmployee?.Email,
            EmployeeRole = p.AssignedEmployee?.Role?.Name,
            Status = p.Status,
            AssignmentType = p.AssignmentType,
            MeasurementRecurrenceId = p.MeasurementRecurrenceId,
            TriggerWaterQualityLogId = p.TriggerWaterQualityLogId,
            UnassignedReason = p.UnassignedReason,
            Revision = p.Revision,
            WaterQualityLogId = p.WaterQualityLogId,
            MeasuredPh = p.WaterQualityLog?.PhValue,
            MeasuredChlorine = p.WaterQualityLog?.FreeChlorineMgL,
            MeasurementStatus = p.WaterQualityLog?.Status,
            MeasuredAt = p.WaterQualityLog?.InspectionTime,
            Notes = p.Notes,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}

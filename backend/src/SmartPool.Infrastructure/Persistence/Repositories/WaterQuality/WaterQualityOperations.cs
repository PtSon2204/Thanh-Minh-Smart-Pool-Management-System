using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;
using SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutySchedules;
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

    private async Task EnsurePoolDutyTableAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS smart_pool.pool_duty_schedules (
                id uuid NOT NULL,
                duty_date date NOT NULL,
                shift_id uuid NOT NULL,
                assigned_employee_id uuid NOT NULL,
                status character varying(50) NOT NULL DEFAULT 'Scheduled',
                assignment_type character varying(50) NOT NULL DEFAULT 'Auto',
                water_quality_log_id uuid NULL,
                notes text NULL,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT pk_pool_duty_schedules PRIMARY KEY (id)
            );
            CREATE INDEX IF NOT EXISTS ix_pool_duty_schedules_duty_date ON smart_pool.pool_duty_schedules(duty_date);
            CREATE INDEX IF NOT EXISTS ix_pool_duty_schedules_employee ON smart_pool.pool_duty_schedules(assigned_employee_id);
            CREATE INDEX IF NOT EXISTS ix_pool_duty_schedules_shift ON smart_pool.pool_duty_schedules(shift_id);
        ";
        try
        {
            await _context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch
        {
            // Ignore if already created or executed in migration
        }
    }

    public async Task<WaterQualityLogDto> CreateWaterQualityLogAsync(WaterQualityLog log, CancellationToken cancellationToken)
    {
        _context.WaterQualityLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        // Tự động gán kết quả đo vào lịch trực hôm nay của nhân viên (nếu có)
        try
        {
            await EnsurePoolDutyTableAsync(cancellationToken);
            var today = DateOnly.FromDateTime(log.InspectionTime);
            var duty = await _context.PoolDutySchedules
                .FirstOrDefaultAsync(d => d.DutyDate == today && d.AssignedEmployeeId == log.InspectedBy && d.WaterQualityLogId == null, cancellationToken);
            if (duty != null)
            {
                duty.WaterQualityLogId = log.Id;
                if (duty.Status == "Scheduled" || duty.Status == "InProgress")
                {
                    duty.Status = "Completed";
                }
                duty.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            // Non-critical auto-link
        }

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

    public async Task<PoolDutyOptionsResponse> GetPoolDutyOptionsAsync(CancellationToken cancellationToken)
    {
        await EnsurePoolDutyTableAsync(cancellationToken);

        var shifts = await _context.Shifts.AsNoTracking()
            .OrderBy(s => s.StartTime)
            .Select(s => new PoolDutyShiftOptionDto
            {
                ShiftId = s.Id,
                Name = s.Name,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            })
            .ToListAsync(cancellationToken);

        var lifeguardQuery = _context.Users.AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.UserProfile)
            .Where(u => u.IsDeleted != true && u.Role != null && u.Role.Name.ToUpper() == "LIFEGUARD");

        var hasLifeguards = await lifeguardQuery.AnyAsync(cancellationToken);
        var staffsQuery = hasLifeguards
            ? lifeguardQuery
            : _context.Users.AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.UserProfile)
                .Where(u => u.IsDeleted != true && u.Role != null && u.Role.Name.ToUpper() != "CUSTOMER");

        var staffs = await staffsQuery
            .OrderBy(u => u.Role!.Name)
            .ThenBy(u => u.Username)
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
        await EnsurePoolDutyTableAsync(cancellationToken);

        // Tự động phân công lịch trực bể nếu khoảng thời gian này chưa có lịch nào
        if (query.FromDate.HasValue && query.ToDate.HasValue && !query.EmployeeId.HasValue && string.IsNullOrWhiteSpace(query.Status))
        {
            var hasAny = await _context.PoolDutySchedules
                .AnyAsync(p => p.DutyDate >= query.FromDate.Value && p.DutyDate <= query.ToDate.Value, cancellationToken);
            if (!hasAny)
            {
                await AutoAssignPoolDutySchedulesAsync(new AutoAssignPoolDutyCommand
                {
                    FromDate = query.FromDate.Value,
                    ToDate = query.ToDate.Value,
                    OverwriteExisting = false,
                    DefaultNotes = "Đo pH, Clo tự do đầu ca và kiểm tra vệ sinh mặt nước bể bơi"
                }, cancellationToken);
            }
        }

        var (page, pageSize) = Normalize(query.PageIndex, query.PageSize);
        var dbQuery = _context.PoolDutySchedules.AsNoTracking()
            .Include(p => p.Shift)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.UserProfile)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.Role)
            .Include(p => p.WaterQualityLog)
            .AsQueryable();

        if (query.FromDate.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.DutyDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.DutyDate <= query.ToDate.Value);
        }

        if (query.ShiftId.HasValue && query.ShiftId.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(p => p.ShiftId == query.ShiftId.Value);
        }

        if (query.EmployeeId.HasValue && query.EmployeeId.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(p => p.AssignedEmployeeId == query.EmployeeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            dbQuery = dbQuery.Where(p => p.Status == query.Status);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .OrderBy(p => p.DutyDate)
            .ThenBy(p => p.Shift != null ? p.Shift.StartTime : TimeOnly.MinValue)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapToPoolDutyDto(p))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PoolDutyScheduleDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<AutoAssignPoolDutyResultDto> AutoAssignPoolDutySchedulesAsync(AutoAssignPoolDutyCommand command, CancellationToken cancellationToken)
    {
        await EnsurePoolDutyTableAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Xác định ca trực cần gán
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

        // Xác định danh sách nhân viên ứng viên
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

        // Tải các lịch trực đã có trong khoảng thời gian
        var existingSchedules = await _context.PoolDutySchedules
            .Where(p => p.DutyDate >= command.FromDate && p.DutyDate <= command.ToDate)
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
                var existing = existingSchedules.FirstOrDefault(e => e.DutyDate == date && e.ShiftId == shift.Id);

                if (existing != null)
                {
                    if (!command.OverwriteExisting || existing.Status == "Completed")
                    {
                        skippedCount++;
                        continue;
                    }

                    // Ghi đè nhân viên trực vòng tròn
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

        // Load relations for response
        var assignedIds = assignedList.Select(a => a.Id).ToList();
        var resultDtos = await _context.PoolDutySchedules.AsNoTracking()
            .Include(p => p.Shift)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.UserProfile)
            .Include(p => p.AssignedEmployee)
                .ThenInclude(u => u!.Role)
            .Where(p => assignedIds.Contains(p.Id))
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
        await EnsurePoolDutyTableAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        PoolDutySchedule schedule;

        if (command.Id.HasValue && command.Id.Value != Guid.Empty)
        {
            schedule = await _context.PoolDutySchedules
                .FirstOrDefaultAsync(p => p.Id == command.Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy lịch trực có mã {command.Id.Value}.");

            // Giữ nguyên DutyDate ban đầu khi cập nhật, không cho phép sửa ngày
            // schedule.DutyDate = command.DutyDate;
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
                .FirstOrDefaultAsync(p => p.DutyDate == command.DutyDate && p.ShiftId == command.ShiftId, cancellationToken);

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
            .FirstAsync(p => p.Id == schedule.Id, cancellationToken);

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
            AssignedEmployeeId = p.AssignedEmployeeId,
            AssignedEmployeeName = p.AssignedEmployee?.UserProfile?.FullName
                ?? p.AssignedEmployee?.Username
                ?? "Chưa xác định",
            AssignedEmployeeEmail = p.AssignedEmployee?.Email,
            EmployeeRole = p.AssignedEmployee?.Role?.Name,
            Status = p.Status,
            AssignmentType = p.AssignmentType,
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

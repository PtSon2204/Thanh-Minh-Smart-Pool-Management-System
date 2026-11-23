using System.Net;
using System.Net.Mail;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using SmartPool.Domain.Constants;

namespace SmartPool.Infrastructure.Persistence.Repositories.WaterQuality;

public sealed partial class WaterQualityOperations
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static readonly JsonSerializerOptions InspectionItemsJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private async Task<WaterQualityLogDto> CreateTaskLinkedWaterQualityLogAsync(WaterQualityLog log,
        OperationalTaskExecutionAuthorization authorization, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        if (!log.InspectedBy.HasValue || log.InspectedBy.Value == Guid.Empty)
            throw new UnauthorizedAccessException("Không xác định được người thực hiện công việc.");
        var taskId = log.OperationalTaskId!.Value;
        var taskReference = await _context.PoolDutySchedules.AsNoTracking()
            .Where(item => item.Id == taskId && item.TaskType != null)
            .Select(item => new { item.AssignedEmployeeId, item.ShiftId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy công việc đã phân công.");
        if (taskReference.AssignedEmployeeId.HasValue)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT user_id FROM smart_pool.employees WHERE user_id = {taskReference.AssignedEmployeeId.Value} FOR UPDATE", cancellationToken);
        if (taskReference.ShiftId.HasValue)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM smart_pool.shifts WHERE id = {taskReference.ShiftId.Value} FOR UPDATE", cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.employee_shifts WHERE id = (SELECT employee_shift_id FROM smart_pool.pool_duty_schedules WHERE id = {taskId}) FOR UPDATE", cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.pool_duty_schedules WHERE id = {taskId} FOR UPDATE", cancellationToken);
        var task = await _context.PoolDutySchedules.Include(item => item.Shift)
            .SingleOrDefaultAsync(item => item.Id == taskId && item.TaskType != null, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy công việc đã phân công.");
        if (task.Status is "Cancelled" or "Completed")
            throw new ValidationException("Công việc đã kết thúc và không thể nhận thêm kết quả.");
        if (authorization.IsAdmin)
        {
            if (!authorization.AdminExecutionConfirmed || !authorization.ExpectedTaskRevision.HasValue ||
                task.Revision != authorization.ExpectedTaskRevision.Value ||
                task.AssignedEmployeeId != authorization.ExpectedAssigneeId)
                throw new InvalidOperationException("Công việc đã thay đổi hoặc Admin chưa xác nhận. Hãy làm mới và xác nhận lại.");
        }
        else if (!authorization.IsLifeguard || !task.AssignedEmployeeId.HasValue ||
                 task.AssignedEmployeeId != log.InspectedBy || !task.EmployeeShiftId.HasValue)
        {
            throw new UnauthorizedAccessException("Chỉ nhân viên được phân công mới có thể nộp kết quả công việc.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (task.TaskType == PoolDutyTaskTypes.Measurement && task.ScheduledAtUtc.HasValue && now < task.ScheduledAtUtc.Value)
            throw new ValidationException("Chưa đến giờ thực hiện công việc đo.");
        if (!authorization.IsAdmin)
        {
            var activeAssignment = await FindEligibleShiftsAsync(now, task.AssignedEmployeeId, task.ShiftId, cancellationToken);
            if (!activeAssignment.Any(item => item.Id == task.EmployeeShiftId))
                throw new UnauthorizedAccessException("Chỉ có thể nộp kết quả trong ca làm đã được phân công.");
        }

        if (task.TaskType == PoolDutyTaskTypes.Measurement)
        {
            if (task.WaterQualityLogId.HasValue)
                throw new ValidationException("Công việc đo này đã có kết quả. Làm mới danh sách công việc.");
            task.Status = "Completed";
            task.WaterQualityLogId = log.Id;
        }
        else if (task.TaskType == PoolDutyTaskTypes.Cleaning)
        {
            if (task.Status != "InProgress" && !(authorization.IsAdmin && task.Status == "Unassigned"))
                throw new ValidationException("Công việc xử lý nước chưa được phân công hoặc chưa bắt đầu.");
            task.WaterQualityLogId = log.Id;
            task.Status = log.Status == "WithinLimits" ? "Completed" : task.Status;
        }
        else
        {
            throw new ValidationException("Loại công việc này không nhận kết quả đo chất lượng nước.");
        }

        task.LastExecutedBy = log.InspectedBy;
        task.LastExecutedAtUtc = now;
        task.LastExecutionAssigneeId = task.AssignedEmployeeId;
        task.LastExecutionWasAdminOverride = authorization.IsAdmin;
        task.Revision++;
        task.UpdatedBy = log.InspectedBy;
        task.UpdatedAt = now;
        _context.WaterQualityLogs.Add(log);

        if (task.TaskType == PoolDutyTaskTypes.Measurement && (log.Status is "Warning" or "Danger"))
        {
            var today = VietnamTimeBoundary.GetDate(_timeProvider);
            var currentShift = await FindEligibleShiftAsync(task.AssignedEmployeeId, today, now, null, cancellationToken);
            var dueAt = currentShift is null ? (DateTime?)null : StaffAttendanceTiming.Interval(currentShift.WorkDate, currentShift.Shift).End;
            var followUp = new PoolDutySchedule
            {
                Id = Guid.NewGuid(),
                TaskType = PoolDutyTaskTypes.Cleaning,
                DutyDate = today,
                ShiftId = currentShift?.ShiftId,
                EmployeeShiftId = currentShift?.Id,
                AssignedEmployeeId = currentShift is null ? null : task.AssignedEmployeeId,
                PreferredEmployeeId = task.AssignedEmployeeId,
                TriggerWaterQualityLogId = log.Id,
                ScheduledAtUtc = now,
                DueAtUtc = dueAt,
                Status = currentShift is null ? "Unassigned" : "InProgress",
                UnassignedReason = currentShift is null ? "NO_ELIGIBLE_CURRENT_SHIFT" : null,
                AssignmentType = "Auto",
                Revision = 1,
                CreatedBy = log.InspectedBy,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.PoolDutySchedules.Add(followUp);
            var content = currentShift is null
                ? $"Kết quả đo ngày {today:dd/MM/yyyy} là {log.Status}. Cần phân công công việc xử lý nước; nhân viên đo hiện không ở trong ca phù hợp."
                : $"Kết quả đo ngày {today:dd/MM/yyyy} là {log.Status}. Hãy đo lại trước khi kết thúc ca để xác nhận nước đã đạt chuẩn.";
            if (currentShift is null)
                await QueueAdminTaskNoticeAsync(followUp, followUp.Id, content, cancellationToken);
            else
            {
                var employee = await _context.Users.AsNoTracking().SingleAsync(item => item.Id == task.AssignedEmployeeId, cancellationToken);
                await QueueTaskNoticeAsync(followUp, followUp.Id, employee.Id, employee.Email, "Cần xử lý chất lượng nước",
                    TaskMessage(followUp, employee.UserProfile?.FullName ?? employee.Username), cancellationToken);
            }
        }
        else if (task.TaskType == PoolDutyTaskTypes.Cleaning && (log.Status is "Warning" or "Danger"))
        {
            var content = $"Kết quả đo lại ngày {task.DutyDate:dd/MM/yyyy} là {log.Status}. Công việc xử lý nước vẫn đang mở; cần tiếp tục xử lý và đo lại.";
            await QueueAdminTaskNoticeAsync(task, task.Id, content, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var created = await _context.WaterQualityLogs.AsNoTracking()
            .Include(item => item.Inspector).ThenInclude(item => item!.UserProfile)
            .Include(item => item.Equipment)
            .SingleAsync(item => item.Id == log.Id, cancellationToken);
        return MapToDto(created);
    }

    public async Task<PagedResponse<PoolDutyScheduleDto>> GetOperationalTasksAsync(
        GetOperationalTasksQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = _context.PoolDutySchedules.AsNoTracking()
            .Where(item => item.TaskType != null)
            .Include(item => item.Shift)
            .Include(item => item.AssignedEmployee).ThenInclude(item => item!.UserProfile)
            .Include(item => item.AssignedEmployee).ThenInclude(item => item!.Role)
            .Include(item => item.WaterQualityLog)
            .AsQueryable();

        if (query.TaskId.HasValue) dbQuery = dbQuery.Where(item => item.Id == query.TaskId.Value);
        if (query.EmployeeId.HasValue) dbQuery = dbQuery.Where(item => item.AssignedEmployeeId == query.EmployeeId);
        if (query.FromDate.HasValue) dbQuery = dbQuery.Where(item => item.DutyDate >= query.FromDate.Value);
        if (query.ToDate.HasValue) dbQuery = dbQuery.Where(item => item.DutyDate <= query.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(query.TaskType)) dbQuery = dbQuery.Where(item => item.TaskType == query.TaskType);
        if (!string.IsNullOrWhiteSpace(query.Status)) dbQuery = dbQuery.Where(item => item.Status == query.Status);

        var (page, pageSize) = Normalize(query.PageIndex, query.PageSize);
        var total = await dbQuery.CountAsync(cancellationToken);
        var tasks = await dbQuery
            .OrderBy(item => item.Status == "Completed" || item.Status == "Cancelled" ? 1 : 0)
            .ThenBy(item => item.DutyDate).ThenBy(item => item.ScheduledAtUtc)
            .ThenBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        var inspectionIds = tasks.Where(item => item.DailyInspectionId.HasValue)
            .Select(item => item.DailyInspectionId!.Value).ToArray();
        var inspections = await _context.DailyInspections.AsNoTracking()
            .Where(item => inspectionIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.OverallStatus,
                item.InspectionTime
            })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var items = tasks.Select(task =>
        {
            var dto = MapToPoolDutyDto(task);
            if (task.DailyInspectionId.HasValue && inspections.TryGetValue(task.DailyInspectionId.Value, out var inspection))
            {
                dto.DailyInspectionId = inspection.Id;
                dto.DailyInspectionStatus = inspection.OverallStatus;
                dto.DailyInspectionAt = inspection.InspectionTime;
            }
            return dto;
        }).ToList();
        return new PagedResponse<PoolDutyScheduleDto>
        {
            Items = items,
            TotalCount = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<PoolDutyMeasurementRecurrenceDto>> GetMeasurementRecurrencesAsync(
        CancellationToken cancellationToken)
    {
        var recurrences = await _context.PoolDutyMeasurementRecurrences.AsNoTracking()
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.NextOccurrenceDate).ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var shiftIds = recurrences.Select(item => item.ShiftId).Distinct().ToArray();
        var employeeIds = recurrences.Select(item => item.PreferredEmployeeId).Distinct().ToArray();
        var shifts = await _context.Shifts.AsNoTracking().Where(item => shiftIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var employees = await _context.Users.AsNoTracking().Where(item => employeeIds.Contains(item.Id))
            .Select(item => new { item.Id, Name = item.UserProfile != null && !string.IsNullOrWhiteSpace(item.UserProfile.FullName)
                ? item.UserProfile.FullName : item.Username ?? "Nhân viên" })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        return recurrences.Select(item => new PoolDutyMeasurementRecurrenceDto(item.Id, item.AnchorDate,
            item.NextOccurrenceDate, item.ScheduledTime, item.ShiftId, shifts.GetValueOrDefault(item.ShiftId)?.Name ?? "Ca không còn tồn tại",
            item.PreferredEmployeeId, employees.GetValueOrDefault(item.PreferredEmployeeId) ?? "Nhân viên không còn tồn tại",
            item.IsActive, item.CreatedAtUtc, item.UpdatedAtUtc)).ToList();
    }

    public async Task<OperationalTaskWeekConfigurationDto?> GetOperationalTaskWeekConfigurationAsync(
        DateOnly weekStart, CancellationToken cancellationToken)
    {
        var monday = WeekMonday(weekStart);
        var configuration = await _context.PoolDutyMeasurementRecurrences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.WeekStart == monday, cancellationToken);
        if (configuration is null) return null;

        return new OperationalTaskWeekConfigurationDto(configuration.Id, monday,
            configuration.AnchorDate.DayNumber - monday.DayNumber + 1, configuration.ScheduledTime,
            configuration.ShiftId, configuration.PreferredEmployeeId);
    }

    public async Task<int> EnsureChecklistTasksAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var today = VietnamTimeBoundary.GetDate(_timeProvider);
        if (date != today)
            throw new InvalidOperationException("Chỉ có thể chuẩn bị công việc kiểm tra thiết bị cho ngày hiện tại.");

        return await EnsureOperationalTasksForRangeAsync(date, date, null, false, cancellationToken);
    }

    public async Task<EnsureOperationalTaskWeekResultDto> EnsureOperationalTaskWeekAsync(EnsureOperationalTaskWeekCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty)
            throw new InvalidOperationException("Không xác định được quản trị viên tạo công việc.");

        var today = VietnamTimeBoundary.GetDate(_timeProvider);
        var daysSinceMonday = ((int)command.WeekStart.DayOfWeek + 6) % 7;
        var monday = command.WeekStart.AddDays(-daysSinceMonday);
        if (monday > DateOnly.MaxValue.AddDays(-6))
            throw new InvalidOperationException("Tuần đã chọn vượt quá ngày tối đa được hỗ trợ.");
        var sunday = monday.AddDays(6);
        if (sunday < today || command.StartDayOfWeek is < 1 or > 7)
            throw new InvalidOperationException("Chỉ có thể chuẩn bị tuần hiện tại hoặc tuần tương lai với thứ bắt đầu từ 1 đến 7.");

        var anchorDate = monday.AddDays(command.StartDayOfWeek - 1);
        if (command.ShiftId == Guid.Empty || command.PreferredEmployeeId == Guid.Empty)
            throw new InvalidOperationException("Ngày đo đầu tiên không được ở quá khứ và cần chọn nhân viên, ca hợp lệ.");

        var shift = await _context.Shifts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == command.ShiftId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy ca làm việc.");
        if (shift.IsActive != true || shift.StartTime == shift.EndTime || !TimeIsWithinShift(command.ScheduledTime, shift))
            throw new InvalidOperationException("Giờ đo phải nằm trong một ca đang hoạt động.");

        var eligibleEmployee = await _context.Employees.AsNoTracking()
            .Include(item => item.User).ThenInclude(item => item!.Role)
            .SingleOrDefaultAsync(item => item.UserId == command.PreferredEmployeeId && item.Status == "Working" &&
                item.User.IsDeleted != true && item.User.Status != null && item.User.Status.ToUpper() == "ACTIVE" &&
                item.User.Role != null && item.User.Role.Name.ToUpper() == "LIFEGUARD", cancellationToken);
        if (eligibleEmployee is null)
            throw new InvalidOperationException("Chỉ có thể chọn nhân viên cứu hộ đang hoạt động.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtextextended('pool-operational-task-materializer', 0))", cancellationToken);

        var configuration = await _context.PoolDutyMeasurementRecurrences
            .SingleOrDefaultAsync(item => item.WeekStart == monday, cancellationToken);
        if (configuration is not null && (configuration.AnchorDate != anchorDate ||
            configuration.ScheduledTime != command.ScheduledTime || configuration.ShiftId != command.ShiftId ||
            configuration.PreferredEmployeeId != command.PreferredEmployeeId))
            throw new StaffConflictException("Tuần này đã có cấu hình đo khác. Không thể thay đổi cấu hình đã chuẩn bị.");

        if (configuration is null)
        {
            if (anchorDate < today)
                throw new InvalidOperationException("Ngày đo đầu tiên không được ở quá khứ và cần chọn nhân viên, ca hợp lệ.");

            var occurrenceDates = new[] { 0, 3, 6 }
                .Where(offset => offset <= sunday.DayNumber - anchorDate.DayNumber)
                .Select(offset => anchorDate.AddDays(offset));
            if (!occurrenceDates.Any(date => LocalToUtc(date, command.ScheduledTime) >= now))
                throw new InvalidOperationException("Tuần đã chọn không còn thời điểm đo nào trong tương lai.");

            configuration = new PoolDutyMeasurementRecurrence
            {
                Id = Guid.NewGuid(),
                WeekStart = monday,
                AnchorDate = anchorDate,
                NextOccurrenceDate = anchorDate,
                ScheduledTime = command.ScheduledTime,
                ShiftId = command.ShiftId,
                PreferredEmployeeId = command.PreferredEmployeeId,
                IsActive = true,
                CreatedBy = command.ActorId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _context.PoolDutyMeasurementRecurrences.Add(configuration);
        }

        var materialized = await MaterializeOperationalTasksForRangeAsync(monday, sunday, command.ActorId,
            [configuration], cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EnsureOperationalTaskWeekResultDto(materialized.CreatedCount, materialized.ExistingCount,
            materialized.UnassignedCount, new OperationalTaskWeekConfigurationDto(configuration.Id, monday,
                command.StartDayOfWeek, command.ScheduledTime, command.ShiftId, command.PreferredEmployeeId));
    }

    public async Task CompleteOperationalChecklistTaskAsync(DailyInspection inspection,
        OperationalTaskExecutionAuthorization authorization, CancellationToken cancellationToken)
    {
        var taskId = inspection.OperationalTaskId
            ?? throw new InvalidOperationException("Công việc kiểm tra thiết bị không hợp lệ.");
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Nộp checklist gắn với công việc cần một giao dịch.");

        var taskReference = await _context.PoolDutySchedules.AsNoTracking()
            .Where(item => item.Id == taskId && item.TaskType == PoolDutyTaskTypes.EquipmentChecklist)
            .Select(item => new { item.AssignedEmployeeId, item.PreferredEmployeeId, item.ShiftId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy công việc kiểm tra thiết bị.");
        var employeeIds = new[] { taskReference.AssignedEmployeeId, taskReference.PreferredEmployeeId }
            .Where(item => item.HasValue).Select(item => item!.Value).Distinct().Order().ToArray();
        foreach (var employeeId in employeeIds)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT user_id FROM smart_pool.employees WHERE user_id = {employeeId} FOR UPDATE", cancellationToken);
        if (taskReference.ShiftId.HasValue)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM smart_pool.shifts WHERE id = {taskReference.ShiftId.Value} FOR UPDATE", cancellationToken);
        foreach (var employeeId in employeeIds)
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM smart_pool.employee_shifts WHERE employee_id = {employeeId}
                ORDER BY id FOR UPDATE
                """, cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.pool_duty_schedules WHERE id = {taskId} FOR UPDATE", cancellationToken);

        var task = _context.PoolDutySchedules.Local.SingleOrDefault(item => item.Id == taskId);
        if (task is null)
            task = await _context.PoolDutySchedules.SingleAsync(item => item.Id == taskId, cancellationToken);
        else
            await _context.Entry(task).ReloadAsync(cancellationToken);

        if (task.TaskType != PoolDutyTaskTypes.EquipmentChecklist ||
            task.DutyDate != inspection.InspectionDate ||
            task.InspectionWindow != (inspection.ShiftId == FixedShifts.Morning.Id
                ? PoolDutyInspectionWindows.Morning : PoolDutyInspectionWindows.Afternoon))
            throw new ValidationException("Công việc không khớp ngày hoặc khung giờ kiểm tra.");
        if (task.Status is "Completed" or "Cancelled")
            throw new InvalidOperationException("Công việc đã kết thúc. Hãy làm mới danh sách.");
        if (task.Revision != authorization.ExpectedTaskRevision ||
            task.AssignedEmployeeId != authorization.ExpectedAssigneeId)
            throw new InvalidOperationException("Công việc đã thay đổi. Hãy làm mới và xác nhận lại.");

        if (authorization.IsAdmin)
        {
            if (!authorization.AdminExecutionConfirmed)
                throw new InvalidOperationException("Admin phải xác nhận trước khi nộp công việc.");
        }
        else
        {
            if (!authorization.IsLifeguard || task.AssignedEmployeeId != inspection.InspectorId ||
                !task.EmployeeShiftId.HasValue || !task.ScheduledAtUtc.HasValue || !task.DueAtUtc.HasValue)
                throw new UnauthorizedAccessException("Chỉ nhân viên được phân công mới có thể nộp kết quả công việc.");

            var candidates = await FindEligibleShiftsAsync(TaskPerformanceTime(task,
                    _timeProvider.GetUtcNow().UtcDateTime), inspection.InspectorId, null, cancellationToken);
            var assignment = candidates.SingleOrDefault(item => item.Id == task.EmployeeShiftId.Value);
            if (assignment is null || assignment.ShiftId != task.ShiftId)
                throw new UnauthorizedAccessException("Ca làm được phân công không còn đủ điều kiện cho công việc này.");
            var interval = StaffAttendanceTiming.Interval(assignment.WorkDate, assignment.Shift);
            if (interval.Start > task.ScheduledAtUtc.Value || interval.End < task.DueAtUtc.Value)
                throw new UnauthorizedAccessException("Ca làm được phân công không bao phủ khung kiểm tra.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        task.LastExecutedBy = inspection.InspectorId;
        task.LastExecutedAtUtc = now;
        task.LastExecutionAssigneeId = task.AssignedEmployeeId;
        task.LastExecutionWasAdminOverride = authorization.IsAdmin;
        task.DailyInspectionId = inspection.Id;
        task.Status = "Completed";
        task.Revision++;
        task.UpdatedBy = inspection.InspectorId;
        task.UpdatedAt = now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ValidationException("Ca kiểm tra đã có biên bản. Hãy làm mới danh sách.");
        }
    }

    private async Task<int> EnsureOperationalTasksForRangeAsync(DateOnly fromDate, DateOnly toDate,
        Guid? actorId, bool includeMeasurements, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtextextended('pool-operational-task-materializer', 0))", cancellationToken);
        var configurations = includeMeasurements
            ? await _context.PoolDutyMeasurementRecurrences.AsNoTracking()
                .Where(item => item.IsActive && item.WeekStart.HasValue && item.WeekStart.Value >= WeekMonday(fromDate) &&
                    item.WeekStart.Value <= WeekMonday(toDate))
                .OrderBy(item => item.WeekStart).ToListAsync(cancellationToken)
            : [];
        var materialized = await MaterializeOperationalTasksForRangeAsync(fromDate, toDate, actorId,
            configurations, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return materialized.CreatedCount;
    }

    private async Task<(int CreatedCount, int ExistingCount, int UnassignedCount)> MaterializeOperationalTasksForRangeAsync(
        DateOnly fromDate, DateOnly toDate, Guid? actorId,
        IReadOnlyCollection<PoolDutyMeasurementRecurrence> configurations, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var created = 0;
        var existing = 0;
        var unassigned = 0;
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            foreach (var window in FixedShifts.All)
            {
                var windowId = window.Id == FixedShifts.Morning.Id
                    ? PoolDutyInspectionWindows.Morning
                    : PoolDutyInspectionWindows.Afternoon;
                var exists = await _context.PoolDutySchedules.AnyAsync(item =>
                    item.TaskType == PoolDutyTaskTypes.EquipmentChecklist && item.DutyDate == date &&
                    item.InspectionWindow == windowId, cancellationToken);
                if (exists)
                {
                    existing++;
                    continue;
                }

                var scheduledAt = LocalToUtc(date, window.StartTime);
                var eligible = await FindEligibleShiftsAsync(scheduledAt, null, null, cancellationToken);
                _context.PoolDutySchedules.Add(new PoolDutySchedule
                {
                    Id = Guid.NewGuid(),
                    TaskType = PoolDutyTaskTypes.EquipmentChecklist,
                    DutyDate = date,
                    InspectionWindow = windowId,
                    ScheduledAtUtc = scheduledAt,
                    DueAtUtc = LocalToUtc(date, window.EndTime),
                    Status = "Unassigned",
                    UnassignedReason = eligible.Count == 0 ? "NO_ELIGIBLE_WORK_SHIFT" : "ADMIN_ASSIGNMENT_REQUIRED",
                    AssignmentType = "Auto",
                    Revision = 1,
                    CreatedBy = actorId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                created++;
            }
        }

        foreach (var recurrence in configurations)
        {
            var weekEnd = recurrence.WeekStart!.Value.AddDays(6);
            var anchor = recurrence.AnchorDate;
            for (var date = anchor; date <= weekEnd; date = date.AddDays(3))
            {
                if (date < fromDate || date > toDate) continue;
                var scheduledAt = LocalToUtc(date, recurrence.ScheduledTime);
                var exists = await _context.PoolDutySchedules.AnyAsync(item =>
                    item.TaskType == PoolDutyTaskTypes.Measurement && item.DutyDate == date &&
                    item.ScheduledAtUtc == scheduledAt && item.ShiftId == recurrence.ShiftId &&
                    (item.AssignedEmployeeId == recurrence.PreferredEmployeeId ||
                     item.PreferredEmployeeId == recurrence.PreferredEmployeeId), cancellationToken);
                if (exists)
                {
                    existing++;
                    continue;
                }

                var eligible = await FindEligibleShiftAsync(recurrence.PreferredEmployeeId, date, scheduledAt,
                    recurrence.ShiftId, cancellationToken);
                _context.PoolDutySchedules.Add(new PoolDutySchedule
                {
                    Id = Guid.NewGuid(),
                    TaskType = PoolDutyTaskTypes.Measurement,
                    DutyDate = date,
                    ShiftId = recurrence.ShiftId,
                    PreferredEmployeeId = recurrence.PreferredEmployeeId,
                    EmployeeShiftId = eligible?.Id,
                    AssignedEmployeeId = eligible?.EmployeeId,
                    MeasurementRecurrenceId = recurrence.Id,
                    ScheduledAtUtc = scheduledAt,
                    Status = eligible is null ? "Unassigned" : "Scheduled",
                    UnassignedReason = eligible is null ? "NO_ELIGIBLE_WORK_SHIFT" : null,
                    AssignmentType = "Auto",
                    Revision = 1,
                    CreatedBy = actorId ?? recurrence.CreatedBy,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                created++;
                if (eligible is null) unassigned++;
            }
        }

        return (created, existing, unassigned);
    }

    private static DateOnly WeekMonday(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    public async Task<IReadOnlyList<PoolDutyEligibleEmployeeDto>> GetOperationalTaskAssigneesAsync(
        Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _context.PoolDutySchedules.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == taskId && item.TaskType != null, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy công việc.");
        if (task.Status is "Completed" or "Cancelled") return [];
        var atUtc = TaskPerformanceTime(task, _timeProvider.GetUtcNow().UtcDateTime);
        var requiredShiftId = task.TaskType == PoolDutyTaskTypes.Measurement ? task.ShiftId : null;
        var candidates = await FindEligibleShiftsAsync(atUtc, null, requiredShiftId, cancellationToken);
        if (task.TaskType == PoolDutyTaskTypes.EquipmentChecklist && task.ScheduledAtUtc.HasValue && task.DueAtUtc.HasValue)
        {
            candidates = candidates.Where(item =>
            {
                var interval = StaffAttendanceTiming.Interval(item.WorkDate, item.Shift);
                return interval.Start <= task.ScheduledAtUtc.Value && interval.End >= task.DueAtUtc.Value;
            }).ToList();
        }
        return candidates.Select(item => new PoolDutyEligibleEmployeeDto
        {
            EmployeeId = item.EmployeeId,
            Name = !string.IsNullOrWhiteSpace(item.Employee.User.UserProfile?.FullName)
                ? item.Employee.User.UserProfile.FullName : item.Employee.User.Username ?? "Nhân viên",
            EmployeeShiftId = item.Id,
            ShiftId = item.ShiftId,
            ShiftName = item.Shift.Name,
            ShiftStartTime = item.Shift.StartTime,
            ShiftEndTime = item.Shift.EndTime
        }).ToList();
    }

    public async Task<DailyInspectionDto?> GetOperationalTaskChecklistResultAsync(Guid taskId, Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var task = await _context.PoolDutySchedules.AsNoTracking()
            .Where(item => item.Id == taskId && item.TaskType == PoolDutyTaskTypes.EquipmentChecklist)
            .Select(item => new { item.AssignedEmployeeId, item.DailyInspectionId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy công việc kiểm tra thiết bị.");
        if (employeeId.HasValue && task.AssignedEmployeeId != employeeId.Value)
            throw new UnauthorizedAccessException("Chỉ nhân viên được phân công mới có thể xem kết quả checklist này.");

        var inspection = await _context.DailyInspections.AsNoTracking()
            .Include(item => item.Inspector).ThenInclude(item => item!.UserProfile)
            .SingleOrDefaultAsync(item => item.Id == task.DailyInspectionId, cancellationToken);
        if (inspection is null) return null;

        List<DailyInspectionItemDto> items;
        try
        {
            items = JsonSerializer.Deserialize<List<DailyInspectionItemDto>>(
                inspection.ItemsJson, InspectionItemsJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            items = [];
        }

        return new DailyInspectionDto
        {
            Id = inspection.Id,
            InspectorId = inspection.InspectorId,
            InspectorName = inspection.Inspector?.UserProfile?.FullName ?? inspection.Inspector?.Username ?? "Nhân viên",
            ShiftId = inspection.ShiftId ?? Guid.Empty,
            ShiftName = inspection.ShiftId.HasValue ? FixedShifts.FindById(inspection.ShiftId.Value)?.Name ?? "Ca trực" : "Chưa phân ca",
            InspectionDate = inspection.InspectionDate,
            InspectionTime = inspection.InspectionTime,
            OverallStatus = inspection.OverallStatus,
            TotalItems = items.Count,
            IssuesCount = items.Count(item => !item.IsPassed),
            Notes = inspection.Notes,
            Items = items
        };
    }

    public async Task<bool> StopMeasurementRecurrenceAsync(StopMeasurementRecurrenceCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.pool_duty_measurement_recurrences WHERE id = {command.RecurrenceId} FOR UPDATE", cancellationToken);
        var recurrence = await _context.PoolDutyMeasurementRecurrences
            .SingleOrDefaultAsync(item => item.Id == command.RecurrenceId, cancellationToken);
        if (recurrence is null) throw new KeyNotFoundException("Không tìm thấy lịch đo định kỳ.");
        if (recurrence.WeekStart.HasValue)
            throw new StaffConflictException("Cấu hình đo của tuần là bất biến và không thể dừng hoặc thay đổi.");
        if (!recurrence.IsActive) return false;
        recurrence.IsActive = false;
        recurrence.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<PoolDutyScheduleDto> AssignOperationalTaskAsync(AssignOperationalTaskCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var taskReference = await _context.PoolDutySchedules.AsNoTracking()
            .Where(item => item.Id == command.TaskId && item.TaskType != null)
            .Select(item => new { item.Id, item.TaskType, item.DutyDate, item.ShiftId, item.AssignedEmployeeId, item.PreferredEmployeeId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy công việc.");
        var assignmentReference = await _context.EmployeeShifts.AsNoTracking()
            .Where(item => item.Id == command.EmployeeShiftId && item.EmployeeId == command.EmployeeId)
            .Select(item => new { item.EmployeeId, item.ShiftId, item.WorkDate })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Ca đã chọn không tồn tại.");
        var employeeIds = new[] { command.EmployeeId, taskReference.AssignedEmployeeId, taskReference.PreferredEmployeeId }
            .Where(item => item.HasValue).Select(item => item!.Value).Distinct().Order().ToArray();
        foreach (var employeeId in employeeIds)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT user_id FROM smart_pool.employees WHERE user_id = {employeeId} FOR UPDATE", cancellationToken);
        foreach (var shiftId in new[] { taskReference.ShiftId, assignmentReference.ShiftId }
            .Where(item => item.HasValue).Select(item => item!.Value).Distinct().Order())
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM smart_pool.shifts WHERE id = {shiftId} FOR UPDATE", cancellationToken);
        foreach (var employeeId in employeeIds)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM smart_pool.employee_shifts WHERE employee_id = {employeeId}
                ORDER BY id FOR UPDATE
                """, cancellationToken);
        }
        foreach (var employeeId in employeeIds)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM smart_pool.pool_duty_schedules WHERE assigned_employee_id = {employeeId}
                ORDER BY id FOR UPDATE
                """, cancellationToken);
        }
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.pool_duty_schedules WHERE id = {command.TaskId} FOR UPDATE", cancellationToken);
        var task = await _context.PoolDutySchedules.Include(item => item.Shift).Include(item => item.AssignedEmployee)
            .ThenInclude(item => item!.UserProfile)
            .SingleOrDefaultAsync(item => item.Id == command.TaskId && item.TaskType != null, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy công việc.");
        if (task.Status == "Cancelled" || task.Status == "Completed")
            throw new InvalidOperationException("Không thể phân công công việc đã kết thúc.");
        if (task.Revision != command.ExpectedRevision)
        {
            if (task.AssignedEmployeeId == command.EmployeeId && task.EmployeeShiftId == command.EmployeeShiftId)
                return MapToPoolDutyDto(task);
            throw new InvalidOperationException("Công việc đã thay đổi. Hãy làm mới danh sách.");
        }

        var candidate = (await FindEligibleShiftsAsync(TaskPerformanceTime(task, _timeProvider.GetUtcNow().UtcDateTime),
                command.EmployeeId, task.TaskType == PoolDutyTaskTypes.Measurement ? task.ShiftId : null, cancellationToken))
            .Where(item => task.TaskType != PoolDutyTaskTypes.EquipmentChecklist ||
                (task.ScheduledAtUtc.HasValue && task.DueAtUtc.HasValue &&
                 StaffAttendanceTiming.Interval(item.WorkDate, item.Shift).Start <= task.ScheduledAtUtc.Value &&
                 StaffAttendanceTiming.Interval(item.WorkDate, item.Shift).End >= task.DueAtUtc.Value))
            .SingleOrDefault(item => item.Id == command.EmployeeShiftId)
            ?? throw new InvalidOperationException("Nhân viên phải là cứu hộ đang làm việc và có ca hợp lệ tại giờ công việc.");

        var oldEmployeeId = task.AssignedEmployeeId;
        task.AssignedEmployeeId = candidate.EmployeeId;
        task.PreferredEmployeeId = candidate.EmployeeId;
        task.EmployeeShiftId = candidate.Id;
        task.ShiftId = candidate.ShiftId;
        task.UnassignedReason = null;
        task.Status = task.TaskType == PoolDutyTaskTypes.Cleaning ? "InProgress" : "Scheduled";
        task.DueAtUtc = task.TaskType == PoolDutyTaskTypes.Cleaning
            ? StaffAttendanceTiming.Interval(candidate.WorkDate, candidate.Shift).End : task.DueAtUtc;
        task.Revision++;
        task.UpdatedBy = command.ActorId;
        task.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var operationId = Guid.NewGuid();
        await QueueTaskNoticeAsync(task, operationId, candidate.EmployeeId, candidate.Employee.User.Email,
            "Bạn được phân công công việc", TaskMessage(task, candidate.Employee.User.UserProfile?.FullName ?? candidate.Employee.User.Username), cancellationToken);
        if (oldEmployeeId.HasValue && oldEmployeeId.Value != candidate.EmployeeId)
        {
            var oldUser = await _context.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == oldEmployeeId.Value, cancellationToken);
            if (oldUser is not null)
                await QueueTaskNoticeAsync(task, operationId, oldUser.Id, oldUser.Email, "Công việc đã được chuyển",
                    $"Công việc ngày {task.DutyDate:dd/MM/yyyy} đã được chuyển cho nhân viên khác.", cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapToPoolDutyDto(task);
    }

    public async Task<PoolDutyScheduleDto> CancelOperationalTaskAsync(CancelOperationalTaskCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var taskReference = await _context.PoolDutySchedules.AsNoTracking()
            .Where(item => item.Id == command.TaskId && item.TaskType != null)
            .Select(item => new { item.Id, item.AssignedEmployeeId, item.PreferredEmployeeId, item.ShiftId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy công việc.");
        foreach (var employeeId in new[] { taskReference.AssignedEmployeeId, taskReference.PreferredEmployeeId }
            .Where(item => item.HasValue).Select(item => item!.Value).Distinct().Order())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT user_id FROM smart_pool.employees WHERE user_id = {employeeId} FOR UPDATE", cancellationToken);
        }
        if (taskReference.ShiftId.HasValue)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM smart_pool.shifts WHERE id = {taskReference.ShiftId.Value} FOR UPDATE", cancellationToken);
        foreach (var employeeId in new[] { taskReference.AssignedEmployeeId, taskReference.PreferredEmployeeId }
            .Where(item => item.HasValue).Select(item => item!.Value).Distinct().Order())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM smart_pool.employee_shifts WHERE employee_id = {employeeId}
                ORDER BY id FOR UPDATE
                """, cancellationToken);
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM smart_pool.pool_duty_schedules WHERE assigned_employee_id = {employeeId}
                ORDER BY id FOR UPDATE
                """, cancellationToken);
        }
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.pool_duty_schedules WHERE id = {command.TaskId} FOR UPDATE", cancellationToken);
        var task = await _context.PoolDutySchedules.Include(item => item.Shift).Include(item => item.AssignedEmployee)
            .ThenInclude(item => item!.UserProfile)
            .SingleOrDefaultAsync(item => item.Id == command.TaskId && item.TaskType != null, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy công việc.");
        if (task.Status == "Cancelled") return MapToPoolDutyDto(task);
        if (task.Revision != command.ExpectedRevision) throw new InvalidOperationException("Công việc đã thay đổi. Hãy làm mới danh sách.");
        if (task.Status == "Completed") throw new InvalidOperationException("Không thể hủy công việc đã hoàn thành.");

        task.Status = "Cancelled";
        task.Revision++;
        task.UpdatedBy = command.ActorId;
        task.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        if (task.AssignedEmployee is not null)
            await QueueTaskNoticeAsync(task, Guid.NewGuid(), task.AssignedEmployeeId, task.AssignedEmployee.Email,
                "Công việc đã được hủy", $"Công việc ngày {task.DutyDate:dd/MM/yyyy} đã được hủy bởi quản trị viên.", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapToPoolDutyDto(task);
    }

    public async Task MaterializeDueOperationalTasksAsync(CancellationToken cancellationToken)
    {
        var today = VietnamTimeBoundary.GetDate(_timeProvider);
        await EnsureOperationalTasksForRangeAsync(today, today, null, true, cancellationToken);
        await QueueDueOperationalTaskNoticesAsync(cancellationToken);
    }

    private async Task QueueDueOperationalTaskNoticesAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var dueTasks = await _context.PoolDutySchedules.FromSqlInterpolated($"""
            SELECT * FROM smart_pool.pool_duty_schedules
            WHERE task_type IS NOT NULL
                AND due_notice_sent_at_utc IS NULL
                AND scheduled_at_utc <= {now}
                AND status NOT IN ('Completed', 'Cancelled')
            ORDER BY scheduled_at_utc, id
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);

        foreach (var task in dueTasks)
        {
            task.DueNoticeSentAtUtc = now;
            if (task.AssignedEmployeeId.HasValue)
            {
                var employee = await _context.Users.AsNoTracking().Include(item => item.UserProfile)
                    .SingleAsync(item => item.Id == task.AssignedEmployeeId.Value, cancellationToken);
                var existingDelivery = await _context.StaffScheduleDeliveries.AnyAsync(item =>
                    item.OperationId == task.Id && item.RecipientId == employee.Id && item.Channel == "EMAIL",
                    cancellationToken);
                if (!existingDelivery)
                    await QueueTaskNoticeAsync(task, task.Id, employee.Id, employee.Email,
                        "Đến giờ thực hiện công việc", TaskMessage(task, employee.UserProfile?.FullName ?? employee.Username),
                        cancellationToken);
            }
            else
            {
                await QueueAdminTaskNoticeAsync(task, task.Id,
                    $"Đã đến giờ thực hiện công việc ngày {task.DutyDate:dd/MM/yyyy}; công việc chưa được phân công.",
                    cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<EmployeeShift?> FindEligibleShiftAsync(Guid? employeeId, DateOnly date, DateTime atUtc,
        Guid? shiftId, CancellationToken cancellationToken)
    {
        return (await FindEligibleShiftsAsync(atUtc, employeeId, shiftId, cancellationToken))
            .FirstOrDefault(item => item.WorkDate <= date && item.WorkDate >= date.AddDays(-1));
    }

    private async Task<List<EmployeeShift>> FindEligibleShiftsAsync(DateTime atUtc, Guid? employeeId, Guid? shiftId,
        CancellationToken cancellationToken)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(atUtc, DateTimeKind.Utc), VietnamTimeZone));
        var candidates = await _context.EmployeeShifts.Include(item => item.Employee).ThenInclude(item => item.User)
            .ThenInclude(item => item!.Role).Include(item => item.Employee).ThenInclude(item => item.User)
            .ThenInclude(item => item!.UserProfile).Include(item => item.Shift)
            .Where(item => item.Status != "Cancelled" && item.WorkDate >= localDate.AddDays(-1) && item.WorkDate <= localDate &&
                (!employeeId.HasValue || item.EmployeeId == employeeId.Value) &&
                (!shiftId.HasValue || item.ShiftId == shiftId.Value) &&
                item.Employee.Status == "Working" && item.Employee.User.IsDeleted != true && item.Employee.User.Status != null &&
                item.Employee.User.Status.ToUpper() == "ACTIVE" && item.Employee.User.Role != null &&
                item.Employee.User.Role.Name.ToUpper() == "LIFEGUARD" && item.Shift.IsActive == true)
            .OrderBy(item => item.WorkDate).ThenBy(item => item.Shift.StartTime).ThenBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);
        return candidates.Where(item =>
        {
            var interval = StaffAttendanceTiming.Interval(item.WorkDate, item.Shift);
            return atUtc >= interval.Start && atUtc < interval.End;
        }).ToList();
    }

    private async Task QueueAdminTaskNoticeAsync(PoolDutySchedule task, Guid operationId, string content,
        CancellationToken cancellationToken)
    {
        var admins = await _context.Users.AsNoTracking().Where(item => item.IsDeleted != true && item.Status != null &&
            item.Status.ToUpper() == "ACTIVE" && item.Role != null && item.Role.Name.ToUpper() == "ADMIN")
            .Select(item => new { item.Id, item.Email }).ToListAsync(cancellationToken);
        var existingDeliveries = await _context.StaffScheduleDeliveries.AsNoTracking()
            .Where(item => item.OperationId == operationId && item.Channel == "EMAIL")
            .Select(item => item.RecipientId).ToListAsync(cancellationToken);
        foreach (var admin in admins)
            if (!existingDeliveries.Contains(admin.Id))
                AddTaskNotice(task, operationId, null, admin.Id, admin.Email, "Cần phân công công việc", content);
    }

    private Task QueueTaskNoticeAsync(PoolDutySchedule task, Guid operationId, Guid? employeeId,
        string? email, string title, string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!employeeId.HasValue)
            throw new InvalidOperationException("Email công việc được gửi cho người nhận gắn với nhân viên.");
        AddTaskNotice(task, operationId, employeeId, employeeId.Value, email, title, content);
        return Task.CompletedTask;
    }

    private void AddTaskNotice(PoolDutySchedule task, Guid operationId, Guid? employeeId, Guid recipientId,
        string? email, string title, string content)
    {
        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = recipientId,
            Title = title,
            Content = content,
            Type = "STAFF_TASK",
            IsRead = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        var validEmail = !string.IsNullOrWhiteSpace(email) && MailAddress.TryCreate(email, out var address) && address?.Address == email;
        var path = $"/admin/staff/tasks?taskId={task.Id:D}";
        _context.StaffScheduleDeliveries.Add(new StaffScheduleDelivery
        {
            OperationId = operationId,
            EmployeeId = employeeId,
            RecipientId = recipientId,
            Channel = "EMAIL",
            RecipientEmail = email ?? "",
            Subject = title,
            HtmlBody = $"<p>{WebUtility.HtmlEncode(content)}</p><p><a href=\"{WebUtility.HtmlEncode(path)}\">Mở công việc</a></p>",
            Status = validEmail ? "Pending" : "Failed",
            AttemptCount = validEmail ? 0 : 3,
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            LastErrorCode = validEmail ? null : "RECIPIENT_EMAIL_INVALID"
        });
    }

    private async Task<PoolDutyMeasurementRecurrenceDto> MapRecurrenceAsync(PoolDutyMeasurementRecurrence recurrence,
        CancellationToken cancellationToken)
    {
        var shift = await _context.Shifts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == recurrence.ShiftId, cancellationToken);
        var employee = await _context.Users.AsNoTracking().Where(item => item.Id == recurrence.PreferredEmployeeId)
            .Select(item => item.UserProfile != null && !string.IsNullOrWhiteSpace(item.UserProfile.FullName)
                ? item.UserProfile.FullName : item.Username ?? "Nhân viên").SingleOrDefaultAsync(cancellationToken);
        return new(recurrence.Id, recurrence.AnchorDate, recurrence.NextOccurrenceDate, recurrence.ScheduledTime,
            recurrence.ShiftId, shift?.Name ?? "Ca không còn tồn tại", recurrence.PreferredEmployeeId,
            employee ?? "Nhân viên không còn tồn tại", recurrence.IsActive, recurrence.CreatedAtUtc, recurrence.UpdatedAtUtc);
    }

    private static bool TimeIsWithinShift(TimeOnly time, Shift shift) => shift.EndTime > shift.StartTime
        ? time >= shift.StartTime && time < shift.EndTime
        : time >= shift.StartTime || time < shift.EndTime;

    private static DateTime LocalToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), VietnamTimeZone);

    private static DateTime TaskPerformanceTime(PoolDutySchedule task, DateTime nowUtc) =>
        task.TaskType is PoolDutyTaskTypes.Cleaning ? nowUtc : task.ScheduledAtUtc ?? LocalToUtc(task.DutyDate,
            task.InspectionWindow == PoolDutyInspectionWindows.Afternoon ? FixedShifts.Afternoon.StartTime : FixedShifts.Morning.StartTime);

    private static string TaskMessage(PoolDutySchedule task, string? employeeName) =>
        $"{employeeName?.Trim() ?? "Nhân viên"}, bạn được giao {(task.TaskType == PoolDutyTaskTypes.Measurement ? "đo chất lượng nước" : task.TaskType == PoolDutyTaskTypes.Cleaning ? "xử lý chất lượng nước" : "kiểm tra thiết bị")} ngày {task.DutyDate:dd/MM/yyyy}.";
}

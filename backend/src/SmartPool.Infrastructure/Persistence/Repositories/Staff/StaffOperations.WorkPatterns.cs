using System.Security.Cryptography;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    private sealed record ScheduleCandidate(DateOnly Date, Shift Shift);
    private sealed record SchedulePlan(ScheduleResult Result, List<EmployeeShift> Cancel,
        List<ScheduleCandidate> Create, List<PoolDutySchedule> Unassign);

    private static readonly JsonSerializerOptions ScheduleJson = new(JsonSerializerDefaults.Web);
    private static string ScheduleSerialize<T>(T value) => JsonSerializer.Serialize(value, ScheduleJson);
    private static T ScheduleDeserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, ScheduleJson)!;
    private static string ScheduleHash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ScheduleSerialize(value))));

    private sealed record SchedulePreview(string Kind, Guid EmployeeId, Guid ActorId,
        string ProposalHash, string StateHash, DateTimeOffset ExpiresAtUtc);

    private string ProtectSchedulePreview(string kind, Guid employeeId, Guid actorId, string proposalHash, string stateHash) =>
        schedulePreviewProtector.Protect(ScheduleSerialize(new SchedulePreview(kind, employeeId, actorId,
            proposalHash, stateHash, timeProvider.GetUtcNow().AddMinutes(15))), TimeSpan.FromMinutes(15));

    private void ValidateSchedulePreview(string? token, string kind, Guid employeeId, Guid actorId,
        string proposalHash, string stateHash)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new StaffOnboardingException("PREVIEW_REQUIRED", 400);
        SchedulePreview? preview;
        try
        {
            preview = JsonSerializer.Deserialize<SchedulePreview>(schedulePreviewProtector.Unprotect(token), ScheduleJson);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            throw new StaffOnboardingException("STALE_PREVIEW");
        }
        if (preview is null || preview.Kind != kind || preview.EmployeeId != employeeId || preview.ActorId != actorId ||
            preview.ExpiresAtUtc <= timeProvider.GetUtcNow() || preview.ProposalHash != proposalHash || preview.StateHash != stateHash)
            throw new StaffOnboardingException("STALE_PREVIEW");
    }

    private IQueryable<StaffScheduleChange> AppliedScheduleChanges => context.StaffScheduleChanges.Where(item => item.AppliedVersion != null);
    private IQueryable<StaffScheduleChange> AppliedPatterns => AppliedScheduleChanges.Where(item => item.Kind == "Pattern");
    private IQueryable<StaffScheduleChange> AppliedExceptions => AppliedScheduleChanges.Where(item => item.Kind != "Pattern");

    public async Task<object> ExecuteWorkPatternAsync(WorkPatternCommand request, CancellationToken cancellationToken)
    {
        await RequireOnboardingAdminAsync(request.ActorId, cancellationToken);
        if (request.Action == WorkPatternAction.Receipt)
            return await ReadScheduleReceiptAsync(request.EmployeeId, request.OperationId, request.ActorId, cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var isRenewal = request.Action is WorkPatternAction.RenewalPreview or WorkPatternAction.RenewalConfirm;
        var isContractChange = isRenewal || request.Action is WorkPatternAction.Preview or WorkPatternAction.Confirm;
        if (request.Action is WorkPatternAction.Confirm or WorkPatternAction.ExceptionConfirm or WorkPatternAction.RenewalConfirm)
            await LockStaffScheduleOperationAsync(request.OperationId, cancellationToken);
        await LockEmployeesAsync([request.EmployeeId], cancellationToken);
        if (isContractChange)
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM smart_pool.users WHERE id = {request.EmployeeId} FOR UPDATE", cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT user_id FROM smart_pool.employees WHERE user_id = {request.EmployeeId} FOR UPDATE", cancellationToken);
        }
        var employee = await context.Employees.Include(item => item.User).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.UserId == request.EmployeeId, cancellationToken)
            ?? throw new StaffOnboardingException("EMPLOYEE_NOT_FOUND", 404);
        var isException = request.Action is WorkPatternAction.ExceptionPreview or WorkPatternAction.ExceptionConfirm;
        if (isContractChange)
        {
            await context.Entry(employee).ReloadAsync(cancellationToken);
            await context.Entry(employee.User).ReloadAsync(cancellationToken);
        }
        var isConfirm = request.Action is WorkPatternAction.Confirm or WorkPatternAction.ExceptionConfirm or WorkPatternAction.RenewalConfirm;
        var previewKind = isRenewal ? "Renewal" : isException ? request.Kind! : "Pattern";
        var proposalHash = isRenewal ? ScheduleHash(new
        {
            Kind = "Renewal", request.EmployeeId, request.ActorId, request.NewStartDate,
            request.NewEndDate, request.ExpectedVersion
        }) : ScheduleHash(new
        {
            request.EmployeeId, request.ActorId, isException, request.Contract, request.EffectiveFrom,
            request.ExpectedVersion, request.Kind, request.WorkDate, request.SourceAssignmentIds,
            request.DestinationShiftId, request.Reason
        });
        if (isConfirm)
        {
            if (request.OperationId == Guid.Empty) throw new StaffOnboardingException("OPERATION_ID_REQUIRED", 400);
            var receipt = await context.StaffScheduleChanges.AsNoTracking().SingleOrDefaultAsync(item => item.OperationId == request.OperationId, cancellationToken);
            if (receipt is not null)
            {
                if (receipt.EmployeeId != request.EmployeeId || receipt.CreatedBy != request.ActorId || receipt.Kind == "Replace" || receipt.ProposalHash != proposalHash)
                    throw new StaffOnboardingException("OPERATION_PAYLOAD_MISMATCH");
                return ScheduleDeserialize<WorkPatternResponse>(receipt.ResponseJson!);
            }
        }
        var state = await LoadScheduleStateAsync(employee, request, cancellationToken);
        if (request.Action == WorkPatternAction.Get)
            return await WorkPatternViewOfAsync(state, cancellationToken);
        if (state.Version != request.ExpectedVersion) throw new StaffOnboardingException("STALE_VERSION");
        if (isRenewal)
            PrepareContractRenewal(request, state);
        else if (!isException)
        {
            await ValidateContractAsync(request.Contract, null, cancellationToken);
            if (request.EffectiveFrom < VietnamTimeBoundary.GetDate(timeProvider))
                throw new StaffOnboardingException("EFFECTIVE_DATE_TOO_SOON", 400);
            if (state.Patterns.Any(item => item.EffectiveFrom > request.EffectiveFrom))
                throw new StaffOnboardingException("EFFECTIVE_DATE_PRECEDES_REVISION", 400);
        }
        var plan = isRenewal ? PlanContractRenewal(request, state) : isException ? PlanException(request, state) : PlanPattern(request, state);
        var stateHash = ScheduleStateHash(state, plan);
        if (!isConfirm)
        {
            var previewToken = ProtectSchedulePreview(previewKind, request.EmployeeId,
                request.ActorId, proposalHash, stateHash);
            await transaction.CommitAsync(cancellationToken);
            return plan.Result with { PreviewToken = previewToken };
        }
        ValidateSchedulePreview(request.PreviewToken, previewKind, request.EmployeeId,
            request.ActorId, proposalHash, stateHash);
        var response = await ApplySchedulePlanAsync(request, state, plan, proposalHash, isException, cancellationToken);
        if (response.Version > state.Version &&
            (isRenewal || isContractChange && request.EffectiveFrom <= state.Today))
            SynchronizeRenewedEmployment(request, state);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private async Task<WorkPatternResponse> ReadScheduleReceiptAsync(Guid employeeId, Guid operationId, Guid actorId, CancellationToken cancellationToken)
    {
        var receipt = await context.StaffScheduleChanges.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId && item.EmployeeId == employeeId &&
                item.CreatedBy == actorId && item.Kind != "Replace", cancellationToken)
            ?? throw new StaffOnboardingException("OPERATION_NOT_FOUND", 404);
        return ScheduleDeserialize<WorkPatternResponse>(receipt.ResponseJson!);
    }

    private async Task ValidateContractAsync(ContractTerms? contract, DateOnly? joinDate, CancellationToken cancellationToken)
    {
        if (contract is null) throw new StaffOnboardingException("CONTRACT_REQUIRED", 400);
        if (contract.EmploymentType is not ("FULL_TIME" or "PART_TIME") || contract.StartDate == default ||
            contract.EndDate < contract.StartDate || (joinDate.HasValue && contract.StartDate != joinDate) ||
            contract.WeeklyPattern is null || contract.WeeklyPattern.Count != 7 ||
            contract.WeeklyPattern.Any(item => item is null || item.DayOfWeek is < 1 or > 7 || item.ShiftIds is null ||
                item.ShiftIds.Contains(Guid.Empty) || item.ShiftIds.Distinct().Count() != item.ShiftIds.Count) ||
            contract.WeeklyPattern.Select(item => item.DayOfWeek).Distinct().Count() != 7 ||
            contract.WeeklyPattern.All(item => item.ShiftIds.Count == 0))
            throw new StaffOnboardingException("INVALID_CONTRACT", 400);
        var ids = contract.WeeklyPattern.SelectMany(item => item.ShiftIds).Distinct().ToArray();
        var shifts = await context.Shifts.AsNoTracking().Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (shifts.Count != ids.Length) throw new StaffOnboardingException("SHIFT_NOT_FOUND", 400);
        if (shifts.Values.Any(item => item.IsActive != true)) throw new StaffOnboardingException("SHIFT_UNAVAILABLE", 400);
        var monday = new DateOnly(2026, 1, 5);
        var week = contract.WeeklyPattern.SelectMany(day => day.ShiftIds.Select(id => (Date: monday.AddDays(day.DayOfWeek - 1), Shift: shifts[id]))).ToList();
        for (var first = 0; first < week.Count; first++)
        {
            if (week[first].Shift.StartTime == week[first].Shift.EndTime) throw new StaffOnboardingException("INVALID_SHIFT_DURATION", 400);
            for (var second = first + 1; second < week.Count; second++)
                if (Overlaps(week[first].Date, week[first].Shift, week[second].Date, week[second].Shift) ||
                    Overlaps(week[first].Date.AddDays(7), week[first].Shift, week[second].Date, week[second].Shift) ||
                    Overlaps(week[first].Date, week[first].Shift, week[second].Date.AddDays(7), week[second].Shift))
                    throw new StaffOnboardingException("PATTERN_OVERLAP", 400);
        }
    }

    private sealed record ScheduleState(Employee Employee, List<StaffScheduleChange> Patterns,
        List<StaffScheduleChange> Exceptions, List<EmployeeShift> Assignments, List<PoolDutySchedule> Duties,
        Dictionary<Guid, Shift> Shifts, DateOnly Today)
    {
        public long Version => Math.Max(Patterns.Select(item => item.AppliedVersion!.Value).DefaultIfEmpty().Max(), Exceptions.Select(item => item.AppliedVersion!.Value).DefaultIfEmpty().Max());
        public ContractTerms? Contract => Patterns.Count == 0 ? null : ScheduleDeserialize<ContractTerms>(Patterns[^1].ContractJson!);
        public ContractTerms? CurrentContract => CurrentPattern is null ? null : ScheduleDeserialize<ContractTerms>(CurrentPattern.ContractJson!);
        public StaffScheduleChange? CurrentPattern => Patterns.Where(item => item.EffectiveFrom <= Today)
            .OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.AppliedVersion).FirstOrDefault()
            ?? Patterns.OrderBy(item => item.EffectiveFrom).ThenByDescending(item => item.AppliedVersion).FirstOrDefault();
        public bool IsPatternGenerated(EmployeeShift assignment) => Patterns.Any(item => item.Id == assignment.ScheduleChangeId);
        public bool Eligible => Employee.Status == "Working" && Employee.User.IsDeleted != true &&
            string.Equals(Employee.User.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ScheduleState> LoadScheduleStateAsync(Employee employee, WorkPatternCommand request, CancellationToken cancellationToken)
    {
        var assignmentShiftIds = await context.EmployeeShifts.Where(item => item.EmployeeId == employee.UserId).Select(item => item.ShiftId).Distinct().ToListAsync(cancellationToken);
        var dutyShiftIds = await context.PoolDutySchedules.Where(item => item.AssignedEmployeeId == employee.UserId && item.ShiftId.HasValue)
            .Select(item => item.ShiftId!.Value).Distinct().ToListAsync(cancellationToken);
        var patterns = await AppliedPatterns.Where(item => item.EmployeeId == employee.UserId).OrderBy(item => item.AppliedVersion).ToListAsync(cancellationToken);
        var patternShiftIds = patterns.SelectMany(item => ScheduleDeserialize<ContractTerms>(item.ContractJson!).WeeklyPattern)
            .SelectMany(item => item.ShiftIds);
        var ids = assignmentShiftIds.Concat(dutyShiftIds).Concat(patternShiftIds).Concat(request.Contract?.WeeklyPattern?.Where(item => item is not null)
            .SelectMany(item => item.ShiftIds ?? []) ?? []).Concat(request.DestinationShiftId.HasValue ? [request.DestinationShiftId.Value] : []).Distinct().Order().ToArray();
        foreach (var id in ids) await LockShiftAsync(id, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM smart_pool.employee_shifts WHERE employee_id = {employee.UserId} ORDER BY id FOR UPDATE", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM smart_pool.pool_duty_schedules WHERE assigned_employee_id = {employee.UserId} ORDER BY id FOR UPDATE", cancellationToken);
        var exceptions = await AppliedExceptions.Where(item => item.EmployeeId == employee.UserId).OrderBy(item => item.AppliedVersion).ToListAsync(cancellationToken);
        var assignments = await context.EmployeeShifts.Include(item => item.Shift).Where(item => item.EmployeeId == employee.UserId).OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var duties = await context.PoolDutySchedules.Include(item => item.Shift).Where(item => item.AssignedEmployeeId == employee.UserId).OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var shifts = await context.Shifts.Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        return new(employee, patterns, exceptions, assignments, duties, shifts, VietnamTimeBoundary.GetDate(timeProvider));
    }

    private async Task<WorkPatternView> WorkPatternViewOfAsync(ScheduleState state, CancellationToken cancellationToken)
    {
        var hours = state.Contract?.WeeklyPattern.SelectMany(item => item.ShiftIds).Sum(id =>
        {
            if (!state.Shifts.TryGetValue(id, out var shift)) return 0m;
            var interval = StaffAttendanceTiming.Interval(new DateOnly(2026, 1, 5), shift);
            return (decimal)(interval.End - interval.Start).TotalHours;
        }) ?? 0m;
        var deliveries = await context.StaffScheduleDeliveries.AsNoTracking().Where(item => item.EmployeeId == state.Employee.UserId)
            .OrderByDescending(item => item.CreatedAtUtc).Take(20)
            .Select(item => new ScheduleDeliveryView(item.Id, item.OperationId, item.Channel, item.Status,
                item.AttemptCount, item.CreatedAtUtc, item.LastErrorCode)).ToListAsync(cancellationToken);
        var replacementEmployeeIds = state.Exceptions.Where(item => item.DestinationEmployeeId.HasValue)
            .Select(item => item.DestinationEmployeeId!.Value).Distinct().ToArray();
        var replacementNames = await context.Users.AsNoTracking().Where(item => replacementEmployeeIds.Contains(item.Id))
            .Select(item => new { item.Id, Name = item.UserProfile != null && !string.IsNullOrWhiteSpace(item.UserProfile.FullName)
                ? item.UserProfile.FullName : item.Username ?? "Nhân viên" })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        return new(state.Contract, state.Version,
            state.Patterns.Select(item => new WorkPatternRevisionView(item.Id, ScheduleDeserialize<ContractTerms>(item.ContractJson!), item.EffectiveFrom!.Value, item.AppliedVersion!.Value, item.CreatedBy, item.CreatedAtUtc)).ToList(),
            state.Exceptions.Select(item => new ScheduleExceptionView(item.Id, item.Kind, item.WorkDate!.Value,
                ScheduleDeserialize<Guid[]>(item.SourceAssignmentIdsJson!), item.DestinationShiftId, item.Reason!, item.AppliedVersion!.Value,
                item.DestinationEmployeeId, item.DestinationEmployeeId.HasValue ? replacementNames.GetValueOrDefault(item.DestinationEmployeeId.Value) : null)).ToList(), hours, deliveries,
            await ContractShiftsAsync(state.Contract, cancellationToken),
            new(state.CurrentContract, state.Today, state.CurrentContract is not null && state.Today > state.CurrentContract.EndDate,
                state.Patterns.Any(item => item.EffectiveFrom > state.Today), state.Employee.Status,
                RenewalAccountActive(state.Employee), state.Employee.Status == "Inactive" && state.Employee.ContractInactiveSinceUtc is null));
    }

    private string ScheduleStateHash(ScheduleState state, SchedulePlan plan) => ScheduleHash(new
    {
        state.Version, state.Eligible, Today = VietnamTimeBoundary.GetDate(timeProvider),
        state.Employee.Status, state.Employee.ContractInactiveSinceUtc, state.Employee.ManualAccessEnabled,
        AccountStatus = state.Employee.User.Status, state.Employee.User.IsDeleted,
        RoleId = state.Employee.User.Role?.Id, RoleDeleted = state.Employee.User.Role?.IsDeleted,
        Patterns = state.Patterns.Select(item => new { item.Id, item.ContractJson, item.EffectiveFrom, item.AppliedVersion }),
        Exceptions = state.Exceptions.Select(item => new { item.Id, item.Kind, item.WorkDate, item.SourceAssignmentIdsJson, item.DestinationShiftId, item.DestinationEmployeeId, item.AppliedVersion }),
        Assignments = state.Assignments.Select(item => new { item.Id, item.ShiftId, item.WorkDate, item.Status, item.CheckInTime, item.CheckOutTime, item.CheckInMethod, item.CheckOutMethod, item.UpdatedAt, item.ScheduleChangeId }),
        Shifts = state.Shifts.Values.OrderBy(item => item.Id).Select(item => new { item.Id, item.Name, item.StartTime, item.EndTime, item.IsActive }),
        Duties = state.Duties.Select(item => new { item.Id, item.ShiftId, item.EmployeeShiftId, item.TaskType, item.DutyDate,
            item.AssignedEmployeeId, item.Status, item.WaterQualityLogId, item.TriggerWaterQualityLogId,
            item.MeasurementRecurrenceId, item.UpdatedAt }),
        plan.Result
    });

    private SchedulePlan PlanPattern(WorkPatternCommand request, ScheduleState state, DateOnly? appendFrom = null)
    {
        var contract = request.Contract ?? throw new StaffOnboardingException("CONTRACT_REQUIRED", 400);
        var details = new List<ScheduleDetail>();
        var create = new List<ScheduleCandidate>();
        var cancel = new List<EmployeeShift>();
        var unassign = new List<PoolDutySchedule>();
        var eligible = appendFrom.HasValue ? RenewalAccountActive(state.Employee) &&
            (state.Employee.Status == "Working" || state.Employee.Status == "Inactive" && state.Employee.ContractInactiveSinceUtc.HasValue) :
            state.Eligible || state.Employee.Status == "Inactive" && state.Employee.ContractInactiveSinceUtc.HasValue &&
                RenewalAccountActive(state.Employee);
        if (!eligible)
        {
            details.Add(new(contract.StartDate, null, "employee_ineligible", "Không tạo lịch vì nhân viên chưa hoạt động."));
            return new(new ScheduleResult(null, 0, 0, 1, 0, details, [], "skipped_ineligible"), cancel, create, unassign);
        }
        var today = VietnamTimeBoundary.GetDate(timeProvider);
        var firstDate = Max(contract.StartDate, request.EffectiveFrom, today.AddDays(1));
        if (appendFrom > firstDate) firstDate = appendFrom.Value;
        var generated = new List<ScheduleCandidate>();

        var excludedStart = appendFrom ?? contract.StartDate;
        var lastExcluded = contract.EndDate < today ? contract.EndDate : today;
        for (var offset = 0; offset <= lastExcluded.DayNumber - excludedStart.DayNumber; offset++)
        {
            var date = excludedStart.AddDays(offset);
            var day = contract.WeeklyPattern.FirstOrDefault(item => item.DayOfWeek == IsoDay(date));
            if (day is not null && day.ShiftIds.Count > 0)
                details.Add(new(date, null, "excluded_today_or_past", "Không tạo ca cho ngày hiện tại hoặc đã qua; lịch bắt đầu từ ngày mai."));
        }

        for (var offset = 0; offset <= contract.EndDate.DayNumber - firstDate.DayNumber; offset++)
        {
            var date = firstDate.AddDays(offset);
            var day = contract.WeeklyPattern.FirstOrDefault(item => item.DayOfWeek == IsoDay(date));
            if (day is null) continue;
            var dateExceptions = state.Exceptions.Where(item => item.WorkDate == date).ToList();
            if (dateExceptions.Any(item => item.Kind == "Leave"))
            {
                details.Add(new(date, null, "exception_preserved", "Đã giữ ngoại lệ đã ghi nhận cho ngày này."));
                continue;
            }
            var suppressedShiftIds = dateExceptions.SelectMany(item => ScheduleDeserialize<Guid[]>(item.SourceAssignmentIdsJson!))
                .Select(id => state.Assignments.SingleOrDefault(assignment => assignment.Id == id)?.ShiftId)
                .Where(id => id.HasValue).Select(id => id!.Value)
                .Concat(dateExceptions.Where(item => item.Kind is "Move" or "Replace")
                    .Where(item => item.DestinationShiftId.HasValue).Select(item => item.DestinationShiftId!.Value))
                .ToHashSet();
            foreach (var shiftId in day.ShiftIds)
            {
                if (suppressedShiftIds.Contains(shiftId))
                {
                    details.Add(new(date, shiftId, "exception_preserved", "Đã giữ ngoại lệ đã ghi nhận cho ca này."));
                    continue;
                }
                if (!state.Shifts.TryGetValue(shiftId, out var shift) || shift.IsActive != true)
                {
                    details.Add(new(date, shiftId, "shift_unavailable", "Mẫu ca không còn hoạt động."));
                    continue;
                }
                generated.Add(new(date, shift));
            }
        }

        var priorGenerated = state.Assignments.Where(item => !appendFrom.HasValue && state.IsPatternGenerated(item) && item.WorkDate > today && item.WorkDate >= request.EffectiveFrom &&
            item.Status == "Scheduled" && item.CheckInTime is null && item.CheckOutTime is null).ToList();
        foreach (var old in priorGenerated)
        {
            var desired = generated.FirstOrDefault(item => item.Date == old.WorkDate && item.Shift.Id == old.ShiftId);
            if (desired is not null) generated.Remove(desired);
            else cancel.Add(old);
        }

        foreach (var candidate in generated)
        {
            var sameDayConflicts = state.Assignments.Where(item => !cancel.Contains(item) && item.Status != "Cancelled")
                .Where(item => item.WorkDate >= candidate.Date.AddDays(-1) && item.WorkDate <= candidate.Date.AddDays(1))
                .ToList();
            var conflict = sameDayConflicts.FirstOrDefault(item => Overlaps(candidate.Date, candidate.Shift, item.WorkDate, item.Shift));
            if (conflict is not null)
            {
                details.Add(new(candidate.Date, candidate.Shift.Id, "overlap", $"Ca bị trùng với lịch {conflict.WorkDate:yyyy-MM-dd}."));
                continue;
            }
            if (state.Assignments.Any(item => item.EmployeeId == request.EmployeeId && item.ShiftId == candidate.Shift.Id && item.WorkDate == candidate.Date))
            {
                details.Add(new(candidate.Date, candidate.Shift.Id, "exact_tuple", "Phân công ca này đã tồn tại, kể cả lịch đã hủy."));
                continue;
            }
            create.Add(candidate);
        }

        foreach (var old in cancel)
        {
            var duties = state.Duties.Where(item => (item.Status is "Scheduled" or "InProgress") &&
                (item.EmployeeShiftId == old.Id || (item.TaskType is null && item.DutyDate == old.WorkDate &&
                    item.ShiftId == old.ShiftId && item.WaterQualityLogId is null))).ToList();
            unassign.AddRange(duties);
            foreach (var duty in duties)
                details.Add(new(duty.DutyDate, duty.ShiftId, "duty_unassigned", "Ca trực đã được gỡ khỏi lịch và cần quản trị viên phân công lại."));
        }

        var conflictCount = details.Count(item => item.Code is "overlap" or "exact_tuple" or "shift_unavailable");
        var skippedCount = details.Count(item => item.Code is "exception_preserved" or "duty_unassigned" or "excluded_today_or_past");
        var protectedCount = appendFrom.HasValue ? state.Assignments.Count(item => item.WorkDate >= today && item.WorkDate <= contract.EndDate) : 0;
        var result = new ScheduleResult(null, create.Count, cancel.Count, skippedCount, conflictCount, details,
            unassign.Select(item => new AffectedScheduleTask(item.Id, item.DutyDate, request.EmployeeId, item.Status)).ToList(),
            appendFrom.HasValue ? conflictCount == 0 ? "completed" : "partial" :
                conflictCount == 0 ? "completed" : create.Count + cancel.Count > 0 ? "partial" : "conflict",
            ProtectedCount: protectedCount);
        return new(result, cancel, create, unassign);
    }

    private static bool RenewalAccountActive(Employee employee) => employee.User.IsDeleted != true &&
        (string.Equals(employee.User.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
         employee.ContractInactiveSinceUtc.HasValue && string.Equals(employee.User.Status, "LOCKED", StringComparison.OrdinalIgnoreCase)) &&
        employee.User.Role is not null && employee.User.Role.IsDeleted != true && IsEligibleEmploymentRole(employee.User.Role.Name);

    private static void PrepareContractRenewal(WorkPatternCommand request, ScheduleState state)
    {
        var previous = state.CurrentContract ?? throw new StaffOnboardingException("RENEWAL_CONTRACT_REQUIRED", 400);
        if (state.Patterns.Any(item => item.EffectiveFrom > state.Today))
            throw new StaffOnboardingException("RENEWAL_PENDING_REVISION");
        if (state.Today < previous.StartDate) throw new StaffOnboardingException("RENEWAL_NOT_STARTED", 400);
        if (state.Today <= previous.EndDate)
        {
            if (request.NewStartDate.HasValue && request.NewStartDate != previous.StartDate)
                throw new StaffOnboardingException("RENEWAL_START_DATE_FIXED", 400);
            if (request.NewEndDate <= previous.EndDate) throw new StaffOnboardingException("INVALID_RENEWAL_DATES", 400);
            request.Contract = previous with { EndDate = request.NewEndDate };
            request.EffectiveFrom = state.Today;
        }
        else
        {
            if (request.NewStartDate is null || request.NewStartDate < state.Today || request.NewStartDate <= previous.EndDate ||
                request.NewEndDate < request.NewStartDate)
                throw new StaffOnboardingException("INVALID_RENEWAL_DATES", 400);
            request.Contract = previous with { StartDate = request.NewStartDate.Value, EndDate = request.NewEndDate };
            request.EffectiveFrom = request.NewStartDate.Value;
        }
    }

    private SchedulePlan PlanContractRenewal(WorkPatternCommand request, ScheduleState state)
    {
        var previous = state.CurrentContract!;
        return PlanPattern(request, state, state.Today <= previous.EndDate ? previous.EndDate.AddDays(1) : request.Contract!.StartDate);
    }

    private void SynchronizeRenewedEmployment(WorkPatternCommand request, ScheduleState state)
    {
        var employee = state.Employee;
        if (employee.ManualAccessEnabled && employee.Status == "Working") return;
        if (!new[] { "SALES", "LIFEGUARD", "COACH" }.Contains(employee.User.Role?.Name, StringComparer.OrdinalIgnoreCase)) return;
        var active = state.Today >= request.EffectiveFrom && state.Today >= request.Contract!.StartDate && state.Today <= request.Contract.EndDate;
        if (employee.Status == "Working" && !active)
        {
            employee.Status = "Inactive";
            var contractOwnsAccountLock = employee.User.IsDeleted != true &&
                string.Equals(employee.User.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            employee.ContractInactiveSinceUtc = contractOwnsAccountLock ? OnboardingNow : null;
            employee.UpdatedAt = OnboardingNow;
            if (contractOwnsAccountLock)
            {
                employee.User.Status = "LOCKED";
                employee.User.UpdatedAt = OnboardingNow;
            }
        }
        else if (employee.Status == "Inactive" && employee.ContractInactiveSinceUtc.HasValue && active &&
            employee.User.IsDeleted != true && string.Equals(employee.User.Status, "LOCKED", StringComparison.OrdinalIgnoreCase) &&
            employee.User.Role is not null && IsEligibleEmploymentRole(employee.User.Role.Name))
        {
            employee.Status = "Working";
            employee.ContractInactiveSinceUtc = null;
            employee.UpdatedAt = OnboardingNow;
            employee.User.Status = "ACTIVE";
            employee.User.UpdatedAt = OnboardingNow;
        }
    }

    private SchedulePlan PlanException(WorkPatternCommand request, ScheduleState state)
    {
        if (request.Kind is not ("Leave" or "Move") || request.WorkDate == default ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            throw new StaffOnboardingException("INVALID_EXCEPTION", 400);
        if (!state.Eligible) return EmptyExceptionPlan(request.WorkDate, "employee_ineligible", "Không thể đổi lịch khi nhân viên chưa hoạt động.");
        var sources = request.SourceAssignmentIds.Distinct().Select(id => state.Assignments.SingleOrDefault(item => item.Id == id)).ToList();
        if (sources.Any(item => item is null) || (request.Kind == "Move" && (sources.Count != 1 || request.DestinationShiftId is null)) ||
            (request.Kind == "Leave" && (sources.Count == 0 || request.DestinationShiftId is not null)) ||
            sources.Any(item => item!.WorkDate != request.WorkDate || item.Status != "Scheduled" || item.CheckInTime is not null || item.CheckOutTime is not null))
            return EmptyExceptionPlan(request.WorkDate, "source_unavailable", "Lịch nguồn không còn phù hợp để sửa.");
        if (request.Kind == "Leave")
        {
            var expectedGeneratedIds = state.Assignments.Where(item => item.WorkDate == request.WorkDate && state.IsPatternGenerated(item) &&
                item.Status == "Scheduled" && item.CheckInTime is null && item.CheckOutTime is null).Select(item => item.Id).ToHashSet();
            if (expectedGeneratedIds.Count == 0 || !expectedGeneratedIds.SetEquals(request.SourceAssignmentIds))
                return EmptyExceptionPlan(request.WorkDate, "leave_requires_all_generated_shifts", "Nghỉ cả ngày phải chọn toàn bộ ca đã sinh từ hợp đồng.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = VietnamTimeBoundary.GetDate(timeProvider);
        foreach (var source in sources.Cast<EmployeeShift>())
        {
            var interval = StaffAttendanceTiming.Interval(source.WorkDate, source.Shift);
            if (source.WorkDate < today || (source.WorkDate == today && now >= interval.Start))
                return EmptyExceptionPlan(request.WorkDate, "shift_started", "Không thể ghi ngoại lệ sau khi ca bắt đầu.");
        }

        var details = new List<ScheduleDetail>();
        var cancel = sources.Cast<EmployeeShift>().ToList();
        var create = new List<ScheduleCandidate>();
        if (request.Kind == "Move")
        {
            var destination = state.Shifts.GetValueOrDefault(request.DestinationShiftId!.Value);
            if (destination is null || destination.IsActive != true)
                return EmptyExceptionPlan(request.WorkDate, "destination_unavailable", "Ca đích không còn hoạt động.");
            var destinationInterval = StaffAttendanceTiming.Interval(request.WorkDate, destination);
            if (request.WorkDate == today && now >= destinationInterval.Start)
                return EmptyExceptionPlan(request.WorkDate, "destination_started", "Ca đích đã bắt đầu.");
            var duplicate = state.Assignments.Any(item => item.ShiftId == destination.Id && item.WorkDate == request.WorkDate);
            var overlap = state.Assignments.Where(item => !cancel.Contains(item) && item.Status != "Cancelled")
                .Any(item => Overlaps(request.WorkDate, destination, item.WorkDate, item.Shift));
            if (duplicate || overlap)
                return EmptyExceptionPlan(request.WorkDate, duplicate ? "exact_tuple" : "overlap", "Ca đích bị trùng hoặc đã có phân công.");
            create.Add(new(request.WorkDate, destination));
        }

        var sourceIds = cancel.Select(item => item.Id).ToHashSet();
        var unassign = state.Duties.Where(item => (item.Status is "Scheduled" or "InProgress") &&
            (item.EmployeeShiftId.HasValue && sourceIds.Contains(item.EmployeeShiftId.Value) ||
             (item.TaskType is null && item.WaterQualityLogId is null && cancel.Any(source =>
                 source.WorkDate == item.DutyDate && source.ShiftId == item.ShiftId)))).ToList();
        foreach (var duty in unassign)
            details.Add(new(duty.DutyDate, duty.ShiftId, "duty_unassigned", "Ca trực đã được gỡ khỏi lịch và cần quản trị viên phân công lại."));
        return new(new ScheduleResult(null, create.Count, cancel.Count, unassign.Count, 0, details,
            unassign.Select(item => new AffectedScheduleTask(item.Id, item.DutyDate, request.EmployeeId, item.Status)).ToList(), "completed"), cancel, create, unassign);
    }

    private static SchedulePlan EmptyExceptionPlan(DateOnly date, string code, string message) =>
        new(new ScheduleResult(null, 0, 0, 0, 1, [new(date, null, code, message)], [], "conflict"), [], [], []);

    private static int IsoDay(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    private static DateOnly Max(DateOnly first, DateOnly second, DateOnly third) =>
        first >= second && first >= third ? first : second >= third ? second : third;

    // Initial contract application for direct Add Employee
    // (plan tm-spms-admin-add-employee §3.5). It only adds the pattern revision, generated
    // shifts and notifications to the caller's transaction; the caller saves and commits.
    private async Task<ScheduleResult> ApplyInitialContractAsync(Employee employee, ContractTerms contract,
        Guid operationId, Guid actorId, CancellationToken cancellationToken)
    {
        var request = new WorkPatternCommand { EmployeeId = employee.UserId, ActorId = actorId,
            Action = WorkPatternAction.Preview, Contract = contract, EffectiveFrom = contract.StartDate, OperationId = operationId };
        var state = await LoadScheduleStateAsync(employee, request, cancellationToken);
        var plan = PlanPattern(request, state);
        var revision = new StaffScheduleChange { EmployeeId = employee.UserId, Kind = "Pattern", ContractJson = ScheduleSerialize(contract),
            EffectiveFrom = contract.StartDate, AppliedVersion = 1, CreatedBy = actorId, CreatedAtUtc = OnboardingNow };
        context.StaffScheduleChanges.Add(revision);
        foreach (var candidate in plan.Create)
            context.EmployeeShifts.Add(new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = employee.UserId,
                ShiftId = candidate.Shift.Id, Shift = candidate.Shift, WorkDate = candidate.Date, Status = "Scheduled",
                ScheduleChangeId = revision.Id, CreatedAt = OnboardingNow, UpdatedAt = OnboardingNow });
        var response = new WorkPatternResponse(contract, revision.AppliedVersion.Value,
            plan.Result with { Status = state.Eligible ? plan.Result.Status : "skipped_ineligible" });
        await QueueScheduleChangeAsync(request, state, plan, response, cancellationToken);
        return response.Result;
    }

    private async Task QueueScheduleChangeAsync(WorkPatternCommand request, ScheduleState state,
        SchedulePlan plan, WorkPatternResponse response, CancellationToken cancellationToken)
    {
        if (plan.Result.CreatedCount == 0 && plan.Result.CancelledCount == 0 && plan.Result.AffectedTasks.Count == 0) return;
        if (request.OperationId == Guid.Empty) throw new StaffOnboardingException("OPERATION_ID_REQUIRED", 400);
        var link = Onboarding.StaffScheduleLink();
        var lines = plan.Create.OrderBy(item => item.Date).ThenBy(item => item.Shift.StartTime)
            .Select(item => $"{item.Date:dd/MM/yyyy}: {item.Shift.Name} ({item.Shift.StartTime:HH\\:mm}-{item.Shift.EndTime:HH\\:mm})").ToList();
        lines.AddRange(plan.Cancel.OrderBy(item => item.WorkDate).ThenBy(item => item.Shift.StartTime)
            .Select(item => $"{item.WorkDate:dd/MM/yyyy}: đã hủy ca {item.Shift.Name} ({item.Shift.StartTime:HH\\:mm}-{item.Shift.EndTime:HH\\:mm})"));
        lines.AddRange(plan.Result.Details.Select(item => $"{item.WorkDate:dd/MM/yyyy}: {item.Message}"));
        var startDate = response.Contract?.StartDate;
        var endDate = response.Contract?.EndDate;
        var summary = $"Lịch làm việc {startDate:dd/MM/yyyy}–{endDate:dd/MM/yyyy}: tạo {plan.Result.CreatedCount} ca, hủy {plan.Result.CancelledCount}, bỏ qua {plan.Result.SkippedCount}, xung đột {plan.Result.ConflictCount}.";
        var employeeMessage = $"{summary} {string.Join("; ", lines)} Mở lịch: {link}";
        var employee = context.Users.Local.SingleOrDefault(item => item.Id == request.EmployeeId);
        if (employee is null)
            employee = await context.Users.AsNoTracking().SingleAsync(item => item.Id == request.EmployeeId, cancellationToken);
        AddScheduleNotification(request, employee.Id, employee.Email, "Cập nhật lịch làm việc", employeeMessage, link);

        if (plan.Result.AffectedTasks.Count == 0) return;
        var admins = await context.Users.AsNoTracking().Where(item => item.IsDeleted != true && item.Status != null &&
            EF.Functions.ILike(item.Status, "ACTIVE") && item.Role != null && item.Role.IsDeleted != true &&
            EF.Functions.ILike(item.Role.Name, "ADMIN")).Select(item => new { item.Id, item.Email }).ToListAsync(cancellationToken);
        var dutyLines = plan.Unassign.Select(item =>
        {
            var shift = item.ShiftId.HasValue ? state.Shifts.GetValueOrDefault(item.ShiftId.Value) : null;
            var taskName = item.TaskType switch
            {
                PoolDutyTaskTypes.Measurement => "đo chất lượng nước",
                PoolDutyTaskTypes.EquipmentChecklist => "kiểm tra thiết bị",
                PoolDutyTaskTypes.Cleaning => "xử lý chất lượng nước",
                _ => "công việc ca trực"
            };
            return shift is null ? $"{item.DutyDate:dd/MM/yyyy}: {taskName}" :
                $"{item.DutyDate:dd/MM/yyyy}: {taskName}, {shift.Name} ({shift.StartTime:HH\\:mm}-{shift.EndTime:HH\\:mm})";
        });
        var adminMessage = $"Thay đổi lịch đã để trống {plan.Result.AffectedTasks.Count} ca trực: {string.Join("; ", dutyLines)}. Vui lòng phân công lại. Mở lịch: {link}";
        foreach (var admin in admins)
            AddScheduleNotification(request, admin.Id, admin.Email, "Cần phân công lại ca trực", adminMessage, link);
    }

    private void AddScheduleNotification(WorkPatternCommand request, Guid recipientId, string? email,
        string title, string content, string link)
    {
        context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = recipientId, Title = title,
            Content = content, Type = "StaffSchedule", IsRead = false, CreatedAt = OnboardingNow });
        var validEmail = !string.IsNullOrWhiteSpace(email) && MailAddress.TryCreate(email, out var address) && address?.Address == email;
        context.StaffScheduleDeliveries.Add(new StaffScheduleDelivery
        {
            OperationId = request.OperationId,
            EmployeeId = request.EmployeeId,
            RecipientId = recipientId,
            Channel = "EMAIL",
            RecipientEmail = email ?? "",
            Subject = title,
            HtmlBody = $"<p>{WebUtility.HtmlEncode(content)}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Mở lịch làm việc</a></p>",
            Status = validEmail ? "Pending" : "Failed",
            AttemptCount = validEmail ? 0 : 3,
            CreatedAtUtc = OnboardingNow,
            LastErrorCode = validEmail ? null : "RECIPIENT_EMAIL_INVALID"
        });
    }

    private async Task<WorkPatternResponse> ApplySchedulePlanAsync(WorkPatternCommand request, ScheduleState state,
        SchedulePlan plan, string proposalHash, bool isException, CancellationToken cancellationToken)
    {
        var version = state.Version;
        var contract = isException ? state.Contract : request.Contract;
        var applied = plan.Result.Status != "conflict";
        var change = new StaffScheduleChange
        {
            EmployeeId = request.EmployeeId, Kind = isException ? request.Kind! : "Pattern",
            AppliedVersion = applied ? ++version : null,
            ContractJson = isException ? null : ScheduleSerialize(contract), EffectiveFrom = isException ? null : request.EffectiveFrom,
            WorkDate = isException ? request.WorkDate : null,
            SourceAssignmentIdsJson = isException ? ScheduleSerialize(request.SourceAssignmentIds) : null,
            DestinationShiftId = isException ? request.DestinationShiftId : null, Reason = isException ? request.Reason!.Trim() : null,
            CreatedBy = request.ActorId, CreatedAtUtc = OnboardingNow, OperationId = request.OperationId, ProposalHash = proposalHash
        };
        context.StaffScheduleChanges.Add(change);
        foreach (var assignment in plan.Cancel)
        {
            assignment.Status = "Cancelled";
            assignment.UpdatedAt = OnboardingNow;
        }
        foreach (var candidate in plan.Create)
            context.EmployeeShifts.Add(new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = request.EmployeeId,
                ShiftId = candidate.Shift.Id, Shift = candidate.Shift, WorkDate = candidate.Date, Status = "Scheduled",
                ScheduleChangeId = change.Id, CreatedAt = OnboardingNow, UpdatedAt = OnboardingNow });
        foreach (var duty in plan.Unassign)
        {
            duty.AssignedEmployeeId = null;
            duty.EmployeeShiftId = null;
            duty.Status = "Unassigned";
            duty.UnassignedReason = "WORK_SCHEDULE_CHANGED";
            if (duty.TaskType == PoolDutyTaskTypes.Cleaning) duty.DueAtUtc = null;
            duty.Revision++;
            duty.UpdatedAt = OnboardingNow;
        }
        var response = new WorkPatternResponse(contract, version, plan.Result with { OperationId = request.OperationId });
        change.ResponseJson = ScheduleSerialize(response);
        await QueueScheduleChangeAsync(request, state, plan, response, cancellationToken);
        return response;
    }
}

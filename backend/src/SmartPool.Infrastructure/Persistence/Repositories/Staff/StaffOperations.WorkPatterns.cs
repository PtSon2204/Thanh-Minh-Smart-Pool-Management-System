using System.Security.Cryptography;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
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

    public async Task<object> ExecuteWorkPatternAsync(WorkPatternCommand request, CancellationToken cancellationToken)
    {
        await RequireOnboardingAdminAsync(request.ActorId, cancellationToken);
        if (request.Action == WorkPatternAction.Receipt)
            return await ReadScheduleReceiptAsync(request.EmployeeId, request.OperationId, cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockEmployeesAsync([request.EmployeeId], cancellationToken);
        var employee = await context.Employees.Include(item => item.User).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.UserId == request.EmployeeId, cancellationToken)
            ?? throw new StaffOnboardingException("EMPLOYEE_NOT_FOUND", 404);
        var isException = request.Action is WorkPatternAction.ExceptionPreview or WorkPatternAction.ExceptionConfirm;
        var isConfirm = request.Action is WorkPatternAction.Confirm or WorkPatternAction.ExceptionConfirm;
        var proposalHash = ScheduleHash(new
        {
            request.EmployeeId, request.ActorId, isException, request.Contract, request.EffectiveFrom,
            request.ExpectedVersion, request.Kind, request.WorkDate, request.SourceAssignmentIds,
            request.DestinationShiftId, request.Reason
        });
        if (isConfirm)
        {
            if (request.OperationId == Guid.Empty) throw new StaffOnboardingException("OPERATION_ID_REQUIRED", 400);
            var receipt = await context.StaffScheduleOperations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            if (receipt is not null)
            {
                if (receipt.EmployeeId != request.EmployeeId || receipt.ProposalHash != proposalHash)
                    throw new StaffOnboardingException("OPERATION_PAYLOAD_MISMATCH");
                return ScheduleDeserialize<WorkPatternResponse>(receipt.ResponseJson);
            }
        }
        var state = await LoadScheduleStateAsync(employee, request, cancellationToken);
        if (request.Action == WorkPatternAction.Get)
            return await WorkPatternViewOfAsync(state, cancellationToken);
        if (state.Version != request.ExpectedVersion) throw new StaffOnboardingException("STALE_VERSION");
        if (!isException)
        {
            await ValidateContractAsync(request.Contract, null, cancellationToken);
            if (request.EffectiveFrom <= VietnamTimeBoundary.GetDate(timeProvider))
                throw new StaffOnboardingException("EFFECTIVE_DATE_TOO_SOON", 400);
            if (state.Patterns.Any(item => item.EffectiveFrom > request.EffectiveFrom))
                throw new StaffOnboardingException("EFFECTIVE_DATE_PRECEDES_REVISION", 400);
        }
        var plan = isException ? PlanException(request, state) : PlanPattern(request, state);
        var stateHash = ScheduleStateHash(state, plan);
        if (!isConfirm)
        {
            var preview = new StaffSchedulePreview { EmployeeId = request.EmployeeId, ActorId = request.ActorId,
                ProposalHash = proposalHash, StateHash = stateHash, ExpiresAtUtc = OnboardingNow.AddMinutes(15) };
            context.StaffSchedulePreviews.Add(preview);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return plan.Result with { PreviewToken = preview.Id.ToString() };
        }
        if (!Guid.TryParse(request.PreviewToken, out var previewId)) throw new StaffOnboardingException("PREVIEW_REQUIRED", 400);
        var storedPreview = await context.StaffSchedulePreviews.SingleOrDefaultAsync(item => item.Id == previewId, cancellationToken);
        if (storedPreview is null || storedPreview.EmployeeId != request.EmployeeId || storedPreview.ActorId != request.ActorId ||
            storedPreview.ExpiresAtUtc <= OnboardingNow || storedPreview.ProposalHash != proposalHash || storedPreview.StateHash != stateHash)
            throw new StaffOnboardingException("STALE_PREVIEW");
        var response = await ApplySchedulePlanAsync(request, state, plan, proposalHash, isException, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private async Task<WorkPatternResponse> ReadScheduleReceiptAsync(Guid employeeId, Guid operationId, CancellationToken cancellationToken)
    {
        var receipt = await context.StaffScheduleOperations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operationId && item.EmployeeId == employeeId, cancellationToken)
            ?? throw new StaffOnboardingException("OPERATION_NOT_FOUND", 404);
        return ScheduleDeserialize<WorkPatternResponse>(receipt.ResponseJson);
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

    private sealed record ScheduleState(Employee Employee, List<StaffWorkPattern> Patterns,
        List<StaffScheduleException> Exceptions, List<EmployeeShift> Assignments, List<PoolDutySchedule> Duties,
        Dictionary<Guid, Shift> Shifts)
    {
        public long Version => Math.Max(Patterns.Select(item => item.Version).DefaultIfEmpty().Max(), Exceptions.Select(item => item.Version).DefaultIfEmpty().Max());
        public ContractTerms? Contract => Patterns.Count == 0 ? null : ScheduleDeserialize<ContractTerms>(Patterns[^1].ContractJson);
        public bool Eligible => Employee.Status == "Working" && Employee.User.IsDeleted != true &&
            string.Equals(Employee.User.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ScheduleState> LoadScheduleStateAsync(Employee employee, WorkPatternCommand request, CancellationToken cancellationToken)
    {
        var assignmentShiftIds = await context.EmployeeShifts.Where(item => item.EmployeeId == employee.UserId).Select(item => item.ShiftId).Distinct().ToListAsync(cancellationToken);
        var dutyShiftIds = await context.PoolDutySchedules.Where(item => item.AssignedEmployeeId == employee.UserId).Select(item => item.ShiftId).Distinct().ToListAsync(cancellationToken);
        var ids = assignmentShiftIds.Concat(dutyShiftIds).Concat(request.Contract?.WeeklyPattern?.Where(item => item is not null)
            .SelectMany(item => item.ShiftIds ?? []) ?? []).Concat(request.DestinationShiftId.HasValue ? [request.DestinationShiftId.Value] : []).Distinct().Order().ToArray();
        foreach (var id in ids) await LockShiftAsync(id, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM smart_pool.employee_shifts WHERE employee_id = {employee.UserId} ORDER BY id FOR UPDATE", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM smart_pool.pool_duty_schedules WHERE assigned_employee_id = {employee.UserId} ORDER BY id FOR UPDATE", cancellationToken);
        var patterns = await context.StaffWorkPatterns.Where(item => item.EmployeeId == employee.UserId).OrderBy(item => item.Version).ToListAsync(cancellationToken);
        var exceptions = await context.StaffScheduleExceptions.Where(item => item.EmployeeId == employee.UserId).OrderBy(item => item.Version).ToListAsync(cancellationToken);
        var assignments = await context.EmployeeShifts.Include(item => item.Shift).Where(item => item.EmployeeId == employee.UserId).OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var duties = await context.PoolDutySchedules.Include(item => item.Shift).Where(item => item.AssignedEmployeeId == employee.UserId).OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var shifts = await context.Shifts.Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        return new(employee, patterns, exceptions, assignments, duties, shifts);
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
            state.Patterns.Select(item => new WorkPatternRevisionView(item.Id, ScheduleDeserialize<ContractTerms>(item.ContractJson), item.EffectiveFrom, item.Version, item.CreatedBy, item.CreatedAtUtc)).ToList(),
            state.Exceptions.Select(item => new ScheduleExceptionView(item.Id, item.Kind, item.WorkDate,
                ScheduleDeserialize<Guid[]>(item.SourceAssignmentIdsJson), item.DestinationShiftId, item.Reason, item.Version,
                item.DestinationEmployeeId, item.DestinationEmployeeId.HasValue ? replacementNames.GetValueOrDefault(item.DestinationEmployeeId.Value) : null)).ToList(), hours, deliveries,
            await ContractShiftsAsync(state.Contract, cancellationToken));
    }

    private string ScheduleStateHash(ScheduleState state, SchedulePlan plan) => ScheduleHash(new
    {
        state.Version, state.Eligible, Today = VietnamTimeBoundary.GetDate(timeProvider),
        Patterns = state.Patterns.Select(item => new { item.Id, item.ContractJson, item.EffectiveFrom, item.Version }),
        Exceptions = state.Exceptions.Select(item => new { item.Id, item.Kind, item.WorkDate, item.SourceAssignmentIdsJson, item.DestinationShiftId, item.Version }),
        Assignments = state.Assignments.Select(item => new { item.Id, item.ShiftId, item.WorkDate, item.Status, item.CheckInTime, item.CheckOutTime, item.CheckInMethod, item.CheckOutMethod, item.UpdatedAt, item.WorkPatternId, item.ScheduleExceptionId }),
        Shifts = state.Shifts.Values.OrderBy(item => item.Id).Select(item => new { item.Id, item.Name, item.StartTime, item.EndTime, item.IsActive }),
        Duties = state.Duties.Select(item => new { item.Id, item.ShiftId, item.DutyDate, item.AssignedEmployeeId, item.Status, item.WaterQualityLogId, item.UpdatedAt }),
        plan.Result
    });

    private SchedulePlan PlanPattern(WorkPatternCommand request, ScheduleState state)
    {
        var contract = request.Contract ?? throw new StaffOnboardingException("CONTRACT_REQUIRED", 400);
        var details = new List<ScheduleDetail>();
        var create = new List<ScheduleCandidate>();
        var cancel = new List<EmployeeShift>();
        var unassign = new List<PoolDutySchedule>();
        if (!state.Eligible)
        {
            details.Add(new(contract.StartDate, null, "employee_ineligible", "Không tạo lịch vì nhân viên chưa hoạt động."));
            return new(new ScheduleResult(null, 0, 0, 1, 0, details, [], "skipped_ineligible"), cancel, create, unassign);
        }
        var today = VietnamTimeBoundary.GetDate(timeProvider);
        var firstDate = Max(contract.StartDate, request.EffectiveFrom, today.AddDays(1));
        var generated = new List<ScheduleCandidate>();

        var lastExcluded = contract.EndDate < today ? contract.EndDate : today;
        for (var offset = 0; offset <= lastExcluded.DayNumber - contract.StartDate.DayNumber; offset++)
        {
            var date = contract.StartDate.AddDays(offset);
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
            var suppressedShiftIds = dateExceptions.SelectMany(item => ScheduleDeserialize<Guid[]>(item.SourceAssignmentIdsJson))
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

        var priorGenerated = state.Assignments.Where(item => item.WorkPatternId.HasValue && item.WorkDate >= request.EffectiveFrom &&
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
            var duties = state.Duties.Where(item => item.DutyDate == old.WorkDate && item.ShiftId == old.ShiftId &&
                item.Status == "Scheduled" && item.WaterQualityLogId is null).ToList();
            unassign.AddRange(duties);
            foreach (var duty in duties)
                details.Add(new(duty.DutyDate, duty.ShiftId, "duty_unassigned", "Ca trực đã được gỡ khỏi lịch và cần quản trị viên phân công lại."));
        }

        var conflictCount = details.Count(item => item.Code is "overlap" or "exact_tuple" or "shift_unavailable");
        var skippedCount = details.Count(item => item.Code is "exception_preserved" or "duty_unassigned" or "excluded_today_or_past");
        var result = new ScheduleResult(null, create.Count, cancel.Count, skippedCount, conflictCount, details,
            unassign.Select(item => new AffectedScheduleTask(item.Id, item.DutyDate, request.EmployeeId, item.Status)).ToList(),
            conflictCount == 0 ? "completed" : create.Count + cancel.Count > 0 ? "partial" : "conflict");
        return new(result, cancel, create, unassign);
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
            var expectedGeneratedIds = state.Assignments.Where(item => item.WorkDate == request.WorkDate && item.WorkPatternId.HasValue &&
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

        var unassign = state.Duties.Where(item => cancel.Any(source => source.WorkDate == item.DutyDate && source.ShiftId == item.ShiftId) &&
            item.Status == "Scheduled" && item.WaterQualityLogId is null).ToList();
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

    private static ScheduleResult LegacyInvitationScheduleResult(DateOnly joinDate) =>
        new(null, 0, 0, 1, 0,
            [new(joinDate, null, "legacy_contract_missing", "Lời mời cũ chưa có điều khoản hợp đồng. Đã duyệt hồ sơ nhưng chưa sinh lịch.")],
            [], "skipped_legacy_no_contract");

    private async Task<ScheduleResult> GenerateApprovedInvitationScheduleAsync(StaffInvitation invitation,
        Guid actorId, CancellationToken cancellationToken)
    {
        var contract = invitation.ContractJson is null
            ? throw new StaffOnboardingException("CONTRACT_REQUIRED", 400)
            : ScheduleDeserialize<ContractTerms>(invitation.ContractJson);
        await ValidateContractAsync(contract, invitation.JoinDate, cancellationToken);
        var employee = context.Employees.Local.SingleOrDefault(item => item.UserId == invitation.CompletedUserId);
        if (employee is null)
            employee = await context.Employees.Include(item => item.User).ThenInclude(item => item.Role)
                .SingleAsync(item => item.UserId == invitation.CompletedUserId, cancellationToken);
        var request = new WorkPatternCommand { EmployeeId = employee.UserId, ActorId = actorId,
            Action = WorkPatternAction.Preview, Contract = contract, EffectiveFrom = contract.StartDate, OperationId = invitation.Id };
        var state = await LoadScheduleStateAsync(employee, request, cancellationToken);
        var plan = PlanPattern(request, state);
        var revision = new StaffWorkPattern { EmployeeId = employee.UserId, ContractJson = ScheduleSerialize(contract),
            EffectiveFrom = contract.StartDate, Version = 1, CreatedBy = actorId, CreatedAtUtc = OnboardingNow };
        context.StaffWorkPatterns.Add(revision);
        foreach (var candidate in plan.Create)
            context.EmployeeShifts.Add(new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = employee.UserId,
                ShiftId = candidate.Shift.Id, Shift = candidate.Shift, WorkDate = candidate.Date, Status = "Scheduled",
                WorkPatternId = revision.Id, CreatedAt = OnboardingNow, UpdatedAt = OnboardingNow });
        var response = new WorkPatternResponse(contract, revision.Version,
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
            var shift = state.Shifts.GetValueOrDefault(item.ShiftId);
            return shift is null ? $"{item.DutyDate:dd/MM/yyyy}: ca {item.ShiftId}" :
                $"{item.DutyDate:dd/MM/yyyy}: {shift.Name} ({shift.StartTime:HH\\:mm}-{shift.EndTime:HH\\:mm})";
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
        Guid? patternId = null;
        Guid? exceptionId = null;
        var contract = isException ? state.Contract : request.Contract;
        if (!isException)
        {
            var revision = new StaffWorkPattern { EmployeeId = request.EmployeeId, ContractJson = ScheduleSerialize(contract),
                EffectiveFrom = request.EffectiveFrom, Version = ++version, CreatedBy = request.ActorId, CreatedAtUtc = OnboardingNow };
            context.StaffWorkPatterns.Add(revision);
            patternId = revision.Id;
        }
        else if (plan.Result.ConflictCount == 0)
        {
            var exception = new StaffScheduleException { EmployeeId = request.EmployeeId, Kind = request.Kind!, WorkDate = request.WorkDate,
                SourceAssignmentIdsJson = ScheduleSerialize(request.SourceAssignmentIds), DestinationShiftId = request.DestinationShiftId,
                Reason = request.Reason!.Trim(), Version = ++version, CreatedBy = request.ActorId, CreatedAtUtc = OnboardingNow };
            context.StaffScheduleExceptions.Add(exception);
            exceptionId = exception.Id;
        }
        foreach (var assignment in plan.Cancel)
        {
            assignment.Status = "Cancelled";
            assignment.UpdatedAt = OnboardingNow;
        }
        foreach (var candidate in plan.Create)
            context.EmployeeShifts.Add(new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = request.EmployeeId,
                ShiftId = candidate.Shift.Id, Shift = candidate.Shift, WorkDate = candidate.Date, Status = "Scheduled",
                WorkPatternId = patternId, ScheduleExceptionId = exceptionId, CreatedAt = OnboardingNow, UpdatedAt = OnboardingNow });
        foreach (var duty in plan.Unassign)
        {
            duty.AssignedEmployeeId = null;
            duty.Status = "Unassigned";
            duty.UpdatedAt = OnboardingNow;
        }
        var response = new WorkPatternResponse(contract, version, plan.Result with { OperationId = request.OperationId });
        context.StaffScheduleOperations.Add(new StaffScheduleOperation { Id = request.OperationId, EmployeeId = request.EmployeeId,
            ActorId = request.ActorId, ProposalHash = proposalHash, ResponseJson = ScheduleSerialize(response), CreatedAtUtc = OnboardingNow });
        await QueueScheduleChangeAsync(request, state, plan, response, cancellationToken);
        return response;
    }
}

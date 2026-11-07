using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    private sealed record StaffShiftReplacementPlan(
        StaffShiftReplacementResult Result,
        EmployeeShift Source,
        Employee? Target,
        EmployeeShift? ExistingTargetAssignment,
        List<PoolDutySchedule> Duties,
        bool CanConfirm,
        long SourceVersion);

    public async Task<StaffShiftReplacementResult> ReplaceStaffShiftAsync(
        ReplaceStaffShiftCommand request, CancellationToken cancellationToken)
    {
        await RequireOnboardingAdminAsync(request.ActorId, cancellationToken);
        if (request.Action == StaffShiftReplacementAction.Receipt)
            return await ReadStaffShiftReplacementReceiptAsync(request.OperationId, request.ActorId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000 ||
            request.ReplacementEmployeeId == Guid.Empty || request.SourceAssignmentId == Guid.Empty)
            throw new StaffOnboardingException("INVALID_REPLACEMENT", 400);

        var proposalHash = ScheduleHash(new
        {
            request.SourceAssignmentId, request.ReplacementEmployeeId, Reason = request.Reason.Trim(), request.ActorId
        });
        if (request.Action == StaffShiftReplacementAction.Confirm)
        {
            if (request.OperationId == Guid.Empty) throw new StaffOnboardingException("OPERATION_ID_REQUIRED", 400);
            var existingReceipt = await context.StaffScheduleOperations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            if (existingReceipt is not null)
            {
                if (existingReceipt.ActorId != request.ActorId || existingReceipt.ProposalHash != proposalHash)
                    throw new StaffOnboardingException("OPERATION_PAYLOAD_MISMATCH");
                return ScheduleDeserialize<StaffShiftReplacementResult>(existingReceipt.ResponseJson);
            }
        }

        var sourceReference = await context.EmployeeShifts.AsNoTracking()
            .Where(item => item.Id == request.SourceAssignmentId)
            .Select(item => new { item.EmployeeId, item.WorkDate, item.ShiftId })
            .SingleOrDefaultAsync(cancellationToken);
        if (sourceReference is null) throw new StaffOnboardingException("ASSIGNMENT_NOT_FOUND", 404);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (request.Action == StaffShiftReplacementAction.Confirm)
        {
            await LockStaffScheduleOperationAsync(request.OperationId, cancellationToken);
            var lockedReceipt = await context.StaffScheduleOperations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            if (lockedReceipt is not null)
            {
                if (lockedReceipt.ActorId != request.ActorId || lockedReceipt.ProposalHash != proposalHash)
                    throw new StaffOnboardingException("OPERATION_PAYLOAD_MISMATCH");
                var replay = ScheduleDeserialize<StaffShiftReplacementResult>(lockedReceipt.ResponseJson);
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }
        }
        await LockEmployeesAsync([sourceReference.EmployeeId, request.ReplacementEmployeeId], cancellationToken);
        await LockShiftAsync(sourceReference.ShiftId, cancellationToken);
        var from = sourceReference.WorkDate.AddDays(-1);
        var through = sourceReference.WorkDate.AddDays(1);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT id FROM smart_pool.employee_shifts
            WHERE employee_id IN ({sourceReference.EmployeeId}, {request.ReplacementEmployeeId})
                AND work_date BETWEEN {from} AND {through}
            ORDER BY employee_id, id FOR UPDATE
            """, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT id FROM smart_pool.pool_duty_schedules
            WHERE assigned_employee_id IN ({sourceReference.EmployeeId}, {request.ReplacementEmployeeId})
                AND duty_date BETWEEN {from} AND {through}
            ORDER BY assigned_employee_id, id FOR UPDATE
            """, cancellationToken);

        var source = await context.EmployeeShifts.Include(item => item.Shift)
            .Include(item => item.Employee).ThenInclude(item => item.User).ThenInclude(item => item.UserProfile)
            .SingleOrDefaultAsync(item => item.Id == request.SourceAssignmentId, cancellationToken)
            ?? throw new StaffOnboardingException("ASSIGNMENT_NOT_FOUND", 404);
        if (source.EmployeeId != sourceReference.EmployeeId || source.WorkDate != sourceReference.WorkDate ||
            source.ShiftId != sourceReference.ShiftId)
            throw new StaffOnboardingException("STALE_PREVIEW");
        var sourceOwnerId = source.EmployeeId;
        var sourceEmployee = await context.Employees.Include(item => item.User).ThenInclude(item => item.UserProfile)
            .Include(item => item.User).ThenInclude(item => item.Role)
            .SingleAsync(item => item.UserId == sourceOwnerId, cancellationToken);
        var target = await EligibleEmployees().Include(item => item.User).ThenInclude(item => item.UserProfile)
            .Include(item => item.User).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.UserId == request.ReplacementEmployeeId, cancellationToken);
        var employeeIds = new[] { sourceOwnerId, request.ReplacementEmployeeId }.Distinct().ToArray();
        var assignments = await context.EmployeeShifts.Include(item => item.Shift)
            .Where(item => employeeIds.Contains(item.EmployeeId) && item.WorkDate >= from && item.WorkDate <= through)
            .OrderBy(item => item.EmployeeId).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var duties = await context.PoolDutySchedules.Include(item => item.Shift)
            .Where(item => employeeIds.Contains(item.AssignedEmployeeId ?? Guid.Empty) && item.DutyDate >= from && item.DutyDate <= through)
            .OrderBy(item => item.AssignedEmployeeId).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var sourceExceptions = await context.StaffScheduleExceptions
            .Where(item => item.EmployeeId == sourceOwnerId && item.WorkDate == source.WorkDate)
            .OrderBy(item => item.Version).ToListAsync(cancellationToken);
        var targetHasLeave = target is not null && await context.StaffScheduleExceptions.AnyAsync(item =>
            item.EmployeeId == target.UserId && item.WorkDate == source.WorkDate && item.Kind == "Leave", cancellationToken);
        var sourcePatterns = await context.StaffWorkPatterns.Where(item => item.EmployeeId == sourceOwnerId)
            .OrderBy(item => item.Version).Select(item => new { item.Id, item.ContractJson, item.EffectiveFrom, item.Version }).ToListAsync(cancellationToken);
        var targetPatterns = await context.StaffWorkPatterns.Where(item => item.EmployeeId == request.ReplacementEmployeeId)
            .OrderBy(item => item.Version).Select(item => new { item.Id, item.ContractJson, item.EffectiveFrom, item.Version }).ToListAsync(cancellationToken);
        var sourceVersion = Math.Max(sourcePatterns.Select(item => item.Version).DefaultIfEmpty().Max(),
            await context.StaffScheduleExceptions.Where(item => item.EmployeeId == sourceOwnerId)
                .Select(item => (long?)item.Version).MaxAsync(cancellationToken) ?? 0);
        var sourceOtherExceptions = await context.StaffScheduleExceptions.Where(item => item.EmployeeId == sourceOwnerId)
            .OrderBy(item => item.Version).Select(item => new { item.Id, item.Kind, item.WorkDate, item.SourceAssignmentIdsJson, item.DestinationShiftId, item.DestinationEmployeeId, item.Version }).ToListAsync(cancellationToken);
        var targetExceptions = await context.StaffScheduleExceptions.Where(item => item.EmployeeId == request.ReplacementEmployeeId)
            .OrderBy(item => item.Version).Select(item => new { item.Id, item.Kind, item.WorkDate, item.SourceAssignmentIdsJson, item.DestinationShiftId, item.DestinationEmployeeId, item.Version }).ToListAsync(cancellationToken);

        var plan = BuildStaffShiftReplacementPlan(request, source, sourceEmployee, target, assignments, duties,
            sourceExceptions, targetHasLeave, sourceVersion);
        var stateHash = ScheduleHash(new
        {
            SourceEligible = sourceEmployee.Status == "Working" && sourceEmployee.User.IsDeleted != true &&
                string.Equals(sourceEmployee.User.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase),
            SourceUserStatus = sourceEmployee.User.Status, SourceRole = sourceEmployee.User.Role?.Name,
            TargetEligible = target is not null && target.Status == "Working" && target.User.IsDeleted != true &&
                string.Equals(target.User.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) && IsEligibleEmploymentRole(target.User.Role?.Name),
            TargetUserStatus = target?.User.Status, TargetEmployeeStatus = target?.Status, TargetRole = target?.User.Role?.Name,
            SourcePatterns = sourcePatterns,
            TargetPatterns = targetPatterns,
            SourceExceptions = sourceOtherExceptions,
            TargetExceptions = targetExceptions,
            TargetHasLeave = targetHasLeave,
            Assignments = assignments.Select(item => new { item.Id, item.EmployeeId, item.ShiftId, item.WorkDate, item.Status,
                item.CheckInTime, item.CheckOutTime, item.UpdatedAt, item.WorkPatternId, item.ScheduleExceptionId,
                item.Shift.StartTime, item.Shift.EndTime, item.Shift.IsActive }),
            Duties = duties.Select(item => new { item.Id, item.AssignedEmployeeId, item.DutyDate, item.ShiftId, item.Status,
                item.WaterQualityLogId, item.UpdatedAt, item.Shift!.StartTime, item.Shift.EndTime }),
            plan.Result.ConflictCode, plan.Result.Status, ResultDuties = plan.Result.Duties
        });

        if (request.Action == StaffShiftReplacementAction.Preview)
        {
            if (!plan.CanConfirm)
            {
                await transaction.RollbackAsync(cancellationToken);
                return plan.Result;
            }
            var preview = new StaffSchedulePreview { EmployeeId = sourceOwnerId, ActorId = request.ActorId,
                ProposalHash = proposalHash, StateHash = stateHash, ExpiresAtUtc = OnboardingNow.AddMinutes(15) };
            context.StaffSchedulePreviews.Add(preview);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return plan.Result with { PreviewToken = preview.Id.ToString() };
        }

        if (request.Action != StaffShiftReplacementAction.Confirm)
            throw new StaffOnboardingException("INVALID_ACTION", 400);
        if (!Guid.TryParse(request.PreviewToken, out var previewId)) throw new StaffOnboardingException("PREVIEW_REQUIRED", 400);
        var storedPreview = await context.StaffSchedulePreviews.SingleOrDefaultAsync(item => item.Id == previewId, cancellationToken);
        if (storedPreview is null || storedPreview.EmployeeId != sourceOwnerId || storedPreview.ActorId != request.ActorId ||
            storedPreview.ExpiresAtUtc <= OnboardingNow || storedPreview.ProposalHash != proposalHash || storedPreview.StateHash != stateHash)
            throw new StaffOnboardingException("STALE_PREVIEW");
        if (!plan.CanConfirm) throw new StaffOnboardingException(plan.Result.ConflictCode ?? "REPLACEMENT_BLOCKED", 409);

        var completed = ApplyStaffShiftReplacement(request, plan, proposalHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return completed;
    }

    private StaffShiftReplacementPlan BuildStaffShiftReplacementPlan(ReplaceStaffShiftCommand request,
        EmployeeShift source, Employee sourceEmployee, Employee? target, List<EmployeeShift> assignments,
        List<PoolDutySchedule> allDuties, List<StaffScheduleException> sourceExceptions,
        bool targetHasLeave, long sourceVersion)
    {
        var targetId = request.ReplacementEmployeeId;
        var originalName = StaffDisplayName(sourceEmployee.User.UserProfile?.FullName, sourceEmployee.User.Username);
        var targetName = target is null ? "" : StaffDisplayName(target.User.UserProfile?.FullName, target.User.Username);
        var targetAssignments = assignments.Where(item => item.EmployeeId == targetId).ToList();
        var exactTarget = targetAssignments.SingleOrDefault(item => item.WorkDate == source.WorkDate && item.ShiftId == source.ShiftId);
        var sourceDuties = allDuties.Where(item => item.AssignedEmployeeId == source.EmployeeId &&
            item.DutyDate == source.WorkDate && item.ShiftId == source.ShiftId).ToList();
        var targetDuties = allDuties.Where(item => item.AssignedEmployeeId == targetId && item.Status != "Cancelled").ToList();
        string? code = null;
        string? message = null;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = VietnamTimeBoundary.GetDate(timeProvider);

        if (source.Status != "Scheduled" || source.CheckInTime is not null || source.CheckOutTime is not null)
            (code, message) = ("source_unavailable", "Lịch nguồn đã thay đổi hoặc đã có chấm công.");
        else if (targetId == source.EmployeeId)
            (code, message) = ("same_employee", "Chọn nhân viên khác với nhân viên hiện tại.");
        else if (target is null || target.User.IsDeleted == true || target.User.Status is null ||
            !string.Equals(target.User.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) || target.Status != "Working" ||
            !IsEligibleEmploymentRole(target.User.Role?.Name))
            (code, message) = ("replacement_unavailable", "Nhân viên thay thế không đủ điều kiện nhận ca.");
        else if (source.Shift.IsActive != true)
            (code, message) = ("shift_unavailable", "Ca làm việc không còn hoạt động.");
        else if (source.WorkDate < today || (source.WorkDate == today && now >= StaffAttendanceTiming.Interval(source.WorkDate, source.Shift).Start))
            (code, message) = ("shift_started", "Không thể thay nhân viên sau khi ca bắt đầu.");
        else if (targetHasLeave)
            (code, message) = ("replacement_on_leave", "Nhân viên thay thế đã có ngoại lệ nghỉ trong ngày này.");
        else if (sourceExceptions.Any(item => item.Kind == "Replace" &&
            ScheduleDeserialize<Guid[]>(item.SourceAssignmentIdsJson).Any(id => id == source.Id)))
            (code, message) = ("exception_exists", "Ca này đã được thay thế trước đó.");
        else if (exactTarget is not null && (exactTarget.Status != "Scheduled" || exactTarget.CheckInTime is not null || exactTarget.CheckOutTime is not null))
            (code, message) = ("destination_assignment_unavailable", "Nhân viên thay thế đã có lịch sử cho đúng ca này.");
        else if (targetAssignments.Any(item => item.Id != exactTarget?.Id && item.Status != "Cancelled" &&
            Overlaps(source.WorkDate, source.Shift, item.WorkDate, item.Shift)))
            (code, message) = ("destination_overlap", "Nhân viên thay thế có ca bị trùng giờ.");
        else if (targetDuties.Any(item => !sourceDuties.Any(sourceDuty => sourceDuty.Id == item.Id) && item.Shift is not null &&
            Overlaps(source.WorkDate, source.Shift, item.DutyDate, item.Shift)))
            (code, message) = ("destination_duty_overlap", "Nhân viên thay thế có ca trực bị trùng giờ.");
        else if (sourceDuties.Any(item => item.Status != "Scheduled" || item.WaterQualityLogId is not null))
            (code, message) = ("duty_unavailable", "Ca trực đã bắt đầu, hoàn tất hoặc có kết quả đo chất lượng nước.");

        var ready = code is null;
        var result = new StaffShiftReplacementResult(source.Id, source.EmployeeId, originalName, targetId, targetName,
            source.WorkDate, source.ShiftId, source.Shift.Name, source.Shift.StartTime, source.Shift.EndTime,
            ready && exactTarget is not null, exactTarget?.Id,
            sourceDuties.Select(item => new PoolDutyTransferView(item.Id, item.DutyDate, item.Status)).ToList(),
            ready ? "ready" : "conflict", code, message);
        return new(result, source, target, exactTarget, sourceDuties, ready, sourceVersion);
    }

    private StaffShiftReplacementResult ApplyStaffShiftReplacement(ReplaceStaffShiftCommand request,
        StaffShiftReplacementPlan plan, string proposalHash)
    {
        var target = plan.Target!;
        var exception = new StaffScheduleException
        {
            Id = Guid.NewGuid(), EmployeeId = plan.Source.EmployeeId, Kind = "Replace",
            WorkDate = plan.Source.WorkDate, SourceAssignmentIdsJson = ScheduleSerialize(new[] { plan.Source.Id }),
            DestinationShiftId = plan.Source.ShiftId, DestinationEmployeeId = target.UserId,
            Reason = request.Reason.Trim(), Version = plan.SourceVersion + 1, CreatedBy = request.ActorId,
            CreatedAtUtc = OnboardingNow
        };
        context.StaffScheduleExceptions.Add(exception);
        plan.Source.Status = "Cancelled";
        plan.Source.UpdatedAt = OnboardingNow;

        var replacement = plan.ExistingTargetAssignment;
        if (replacement is null)
        {
            replacement = new EmployeeShift
            {
                Id = Guid.NewGuid(), EmployeeId = target.UserId, ShiftId = plan.Source.ShiftId,
                Shift = plan.Source.Shift, WorkDate = plan.Source.WorkDate, Status = "Scheduled",
                ScheduleExceptionId = exception.Id, CreatedAt = OnboardingNow, UpdatedAt = OnboardingNow
            };
            context.EmployeeShifts.Add(replacement);
        }
        foreach (var duty in plan.Duties)
        {
            duty.AssignedEmployeeId = target.UserId;
            duty.UpdatedAt = OnboardingNow;
        }

        var completed = plan.Result with
        {
            Status = "completed", ConflictCode = null, Message = null,
            ReplacementAssignmentId = replacement.Id, OperationId = request.OperationId
        };
        var link = Onboarding.StaffScheduleLink();
        var time = $"{plan.Source.Shift.StartTime:HH\\:mm}–{plan.Source.Shift.EndTime:HH\\:mm}";
        var summary = $"{plan.Source.WorkDate:dd/MM/yyyy}, {plan.Source.Shift.Name} ({time}). Lý do: {request.Reason.Trim()}.";
        AddReplacementNotification(request.OperationId, plan.Source.EmployeeId, plan.Source.EmployeeId,
            plan.Source.Employee.User.Email, "Ca làm việc đã được chuyển", $"Ca làm việc của bạn đã được chuyển cho nhân viên khác. {summary} Mở lịch: {link}");
        AddReplacementNotification(request.OperationId, target.UserId, target.UserId, target.User.Email,
            "Bạn được phân công thay ca", $"Bạn được phân công nhận ca làm việc. {summary} Mở lịch: {link}");

        context.StaffScheduleOperations.Add(new StaffScheduleOperation
        {
            Id = request.OperationId, EmployeeId = plan.Source.EmployeeId, ActorId = request.ActorId,
            ProposalHash = proposalHash, ResponseJson = ScheduleSerialize(completed), CreatedAtUtc = OnboardingNow
        });
        return completed;
    }

    private void AddReplacementNotification(Guid operationId, Guid employeeId, Guid recipientId,
        string? email, string subject, string content)
    {
        context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), UserId = recipientId, Title = subject, Content = content,
            Type = "StaffSchedule", IsRead = false, CreatedAt = OnboardingNow
        });
        var validEmail = !string.IsNullOrWhiteSpace(email) && MailAddress.TryCreate(email, out var address) && address?.Address == email;
        context.StaffScheduleDeliveries.Add(new StaffScheduleDelivery
        {
            OperationId = operationId, EmployeeId = employeeId, RecipientId = recipientId,
            Channel = "EMAIL", RecipientEmail = email ?? "", Subject = subject,
            HtmlBody = $"<p>{WebUtility.HtmlEncode(content)}</p><p><a href=\"{WebUtility.HtmlEncode(Onboarding.StaffScheduleLink())}\">Mở lịch làm việc</a></p>",
            Status = validEmail ? "Pending" : "Failed", AttemptCount = validEmail ? 0 : 3,
            CreatedAtUtc = OnboardingNow, LastErrorCode = validEmail ? null : "RECIPIENT_EMAIL_INVALID"
        });
    }

    private async Task<StaffShiftReplacementResult> ReadStaffShiftReplacementReceiptAsync(
        Guid operationId, Guid actorId, CancellationToken cancellationToken)
    {
        var receipt = await context.StaffScheduleOperations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operationId && item.ActorId == actorId, cancellationToken)
            ?? throw new StaffOnboardingException("OPERATION_NOT_FOUND", 404);
        var result = ScheduleDeserialize<StaffShiftReplacementResult>(receipt.ResponseJson);
        if (result.Status != "completed") throw new StaffOnboardingException("OPERATION_NOT_FOUND", 404);
        return result;
    }

    private Task LockStaffScheduleOperationAsync(Guid operationId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"staff-schedule-operation:{operationId:N}"}, 0))",
            cancellationToken);

    private static string StaffDisplayName(string? fullName, string? username) =>
        !string.IsNullOrWhiteSpace(fullName) ? fullName : username ?? "Nhân viên";
}

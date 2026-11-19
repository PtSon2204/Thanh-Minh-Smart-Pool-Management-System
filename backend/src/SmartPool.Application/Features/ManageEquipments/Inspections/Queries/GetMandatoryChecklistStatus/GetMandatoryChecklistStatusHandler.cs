using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetMandatoryChecklistStatus;

public sealed class GetMandatoryChecklistStatusHandler(
    IWaterQualityOperations waterQualityOperations,
    IDailyInspectionOperations inspectionOperations,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetMandatoryChecklistStatusQuery, MandatoryChecklistStatusDto>
{
    public async Task<MandatoryChecklistStatusDto> Handle(
        GetMandatoryChecklistStatusQuery request,
        CancellationToken cancellationToken)
    {
        var nowVietnam = VietnamTimeBoundary.GetVietnamNow(timeProvider);
        var today = VietnamTimeBoundary.GetDate(timeProvider);
        var currentTime = VietnamTimeBoundary.GetTime(timeProvider);
        var serverTime = nowVietnam.ToString("HH:mm:ss dd/MM/yyyy");

        // 1. Chỉ áp dụng cho tài khoản Cứu hộ (LIFEGUARD)
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || !currentUser.IsInRole("LIFEGUARD"))
        {
            return new MandatoryChecklistStatusDto
            {
                HasPendingMandatoryChecklist = false,
                ServerTime = serverTime
            };
        }

        var userId = currentUser.UserId.Value;

        // 2. Lấy các công việc kiểm tra thiết bị được gán cho nhân viên cứu hộ hôm nay
        var tasksResult = await waterQualityOperations.GetOperationalTasksAsync(
            new GetOperationalTasksQuery(
                FromDate: today,
                ToDate: today,
                TaskType: PoolDutyTaskTypes.EquipmentChecklist)
            {
                EmployeeId = userId,
                PageSize = 10
            },
            cancellationToken);

        var activeTasks = tasksResult.Items
            .Where(t => t.Status != "Completed" && t.Status != "Cancelled")
            .ToList();

        if (activeTasks.Count == 0)
        {
            return new MandatoryChecklistStatusDto
            {
                HasPendingMandatoryChecklist = false,
                ServerTime = serverTime
            };
        }

        // 3. Kiểm tra xem có task nào đang nằm trong khung giờ kiểm tra và ca trực chưa nộp checklist
        foreach (var task in activeTasks)
        {
            FixedShiftDefinition? shiftDef = null;
            if (task.ShiftId.HasValue)
            {
                shiftDef = FixedShifts.FindById(task.ShiftId.Value);
            }

            if (shiftDef == null && !string.IsNullOrWhiteSpace(task.InspectionWindow))
            {
                shiftDef = string.Equals(task.InspectionWindow, PoolDutyInspectionWindows.Morning, StringComparison.OrdinalIgnoreCase)
                    ? FixedShifts.Morning
                    : FixedShifts.Afternoon;
            }

            if (shiftDef == null) continue;

            // Kiểm tra khung giờ mở kiểm tra (trước ca 30 phút đến khi ca kết thúc)
            if (!shiftDef.IsInInspectionWindow(currentTime))
            {
                continue;
            }

            // Kiểm tra xem ca này trong ngày hôm nay đã có biên bản nộp chưa
            var existingInspection = await inspectionOperations.GetByDateAndShiftAsync(today, shiftDef.Id, cancellationToken);
            if (existingInspection != null)
            {
                // Ca này đã được ai đó (hoặc Admin) nộp rồi
                continue;
            }

            // Tìm thấy task bắt buộc cần nộp checklist ngay!
            return new MandatoryChecklistStatusDto
            {
                HasPendingMandatoryChecklist = true,
                TaskId = task.Id,
                ShiftId = shiftDef.Id,
                ShiftName = shiftDef.Name,
                InspectionWindow = task.InspectionWindow ?? (shiftDef.Id == FixedShifts.Morning.Id ? PoolDutyInspectionWindows.Morning : PoolDutyInspectionWindows.Afternoon),
                TimeRange = $"{shiftDef.StartTime:HH\\:mm} - {shiftDef.EndTime:HH\\:mm}",
                AllowedFromTime = shiftDef.AllowedFromTime.ToString("HH\\:mm"),
                AllowedToTime = shiftDef.AllowedToTime.ToString("HH\\:mm"),
                TaskRevision = task.Revision,
                ServerTime = serverTime
            };
        }

        return new MandatoryChecklistStatusDto
        {
            HasPendingMandatoryChecklist = false,
            ServerTime = serverTime
        };
    }
}

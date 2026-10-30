using MediatR;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspectionForm;

public sealed class GetDailyInspectionFormHandler(
    IRepository<Equipment> equipmentRepository,
    IRepository<DailyInspection> inspectionRepository,
    IRepository<User> userRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetDailyInspectionFormQuery, DailyInspectionFormResponseDto>
{
    public async Task<DailyInspectionFormResponseDto> Handle(
        GetDailyInspectionFormQuery request,
        CancellationToken cancellationToken)
    {
        var today = VietnamTimeBoundary.GetDate(timeProvider);
        var currentTime = VietnamTimeBoundary.GetTime(timeProvider);
        var vietnamNow = VietnamTimeBoundary.GetVietnamNow(timeProvider);

        // 1. Equipments to inspect
        var equipments = await equipmentRepository.FindAsync(
            x => x.IsDeleted != true,
            cancellationToken);

        var equipmentDtos = equipments
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Select(x => new DailyInspectionFormItemDto
            {
                EquipmentId = x.Id,
                Name = x.Name,
                Category = x.Category,
                Location = x.Location,
                Status = x.Status ?? "Active"
            })
            .ToList();

        // 2. Today's inspections to check duplicate submissions
        var todayInspections = await inspectionRepository.FindAsync(
            x => x.InspectionDate == today,
            cancellationToken);

        var shiftDtos = new List<ShiftInspectionStatusDto>();

        foreach (var shift in FixedShifts.All)
        {
            var existing = todayInspections.FirstOrDefault(i => i.ShiftId == shift.Id);
            var isAlreadySubmitted = existing != null;
            var isOpenNow = shift.IsInInspectionWindow(currentTime);

            string? submittedByName = null;
            if (existing != null)
            {
                var inspector = await userRepository.GetByIdAsync(existing.InspectorId, cancellationToken);
                submittedByName = inspector?.UserProfile?.FullName ?? inspector?.Username ?? "Cứu hộ";
            }

            string statusMessage;
            if (isAlreadySubmitted)
            {
                statusMessage = $"Đã kiểm tra lúc {existing!.InspectionTime:HH:mm} bởi {submittedByName}";
            }
            else if (currentTime < shift.AllowedFromTime)
            {
                statusMessage = $"Chưa mở (Chỉ mở trước ca 30 phút, từ {shift.AllowedFromTime:HH:mm})";
            }
            else if (currentTime > shift.AllowedToTime)
            {
                statusMessage = $"Đã kết thúc ca làm lúc {shift.AllowedToTime:HH:mm}";
            }
            else
            {
                statusMessage = $"Đang mở kiểm tra ({shift.AllowedFromTime:HH:mm} - {shift.AllowedToTime:HH:mm})";
            }

            shiftDtos.Add(new ShiftInspectionStatusDto
            {
                ShiftId = shift.Id,
                Name = shift.Name,
                TimeRange = $"{shift.StartTime:HH:mm} - {shift.EndTime:HH:mm}",
                StartTime = shift.StartTime.ToString("HH:mm"),
                EndTime = shift.EndTime.ToString("HH:mm"),
                AllowedFromTime = shift.AllowedFromTime.ToString("HH:mm"),
                AllowedToTime = shift.AllowedToTime.ToString("HH:mm"),
                IsOpenNow = isOpenNow,
                IsAlreadySubmittedToday = isAlreadySubmitted,
                SubmittedAt = existing?.InspectionTime,
                SubmittedByName = submittedByName,
                StatusMessage = statusMessage
            });
        }

        // Determine current open shift
        var activeShift = shiftDtos.FirstOrDefault(s => s.IsOpenNow && !s.IsAlreadySubmittedToday)
            ?? shiftDtos.FirstOrDefault(s => s.IsOpenNow);

        var canSubmit = shiftDtos.Any(s => s.IsOpenNow && !s.IsAlreadySubmittedToday);
        string? disableReason = null;

        if (!canSubmit)
        {
            if (shiftDtos.All(s => s.IsAlreadySubmittedToday))
            {
                disableReason = "Tất cả các ca trực hôm nay đều đã hoàn thành kiểm tra thiết bị đầu ca.";
            }
            else if (shiftDtos.All(s => !s.IsOpenNow))
            {
                disableReason = $"Hiện tại ({vietnamNow:HH:mm}) không nằm trong khung giờ kiểm tra. Hệ thống chỉ cho phép nộp biên bản trước ca trực 30 phút (Ca sáng: 05:30 - 11:30, Ca chiều: 13:00 - 19:30).";
            }
            else
            {
                disableReason = "Ca trực hiện tại đã có biên bản kiểm tra. Mỗi ca trực chỉ kiểm tra 1 lần.";
            }
        }

        return new DailyInspectionFormResponseDto
        {
            Equipments = equipmentDtos,
            Shifts = shiftDtos,
            CurrentShiftId = activeShift?.ShiftId,
            CanSubmit = canSubmit,
            DisableReason = disableReason,
            ServerTime = vietnamNow.ToString("HH:mm:ss dd/MM/yyyy")
        };
    }
}

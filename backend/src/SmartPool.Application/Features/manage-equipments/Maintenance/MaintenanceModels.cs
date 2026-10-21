using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

public sealed class MaintenanceRequest
{
    public Guid EquipmentId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string RepeatUnit { get; set; } = "None";
    public int RepeatEvery { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public Guid AssignedTo { get; set; }
    public int RemindBeforeDays { get; set; } = 3;
}

public sealed class MaintenanceRequestValidator : AbstractValidator<MaintenanceRequest>
{
    public MaintenanceRequestValidator()
    {
        RuleFor(x => x.EquipmentId).NotEmpty().WithMessage("Chọn thiết bị.");
        RuleFor(x => x.TaskName).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("Nhập tên công việc.")
            .Must(x => x.Trim().Length <= 255 && !x.Contains('\0')).WithMessage("Tên công việc tối đa 255 ký tự và không chứa ký tự không hợp lệ.");
        RuleFor(x => x.RepeatUnit).Must(x => x is "None" or "Day" or "Week" or "Month").WithMessage("Chọn chu kỳ bảo trì hợp lệ.");
        RuleFor(x => x.RepeatEvery).InclusiveBetween(1, 365).WithMessage("Chu kỳ lặp từ 1 đến 365.");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("Chọn ngày bắt đầu.");
        RuleFor(x => x.AssignedTo).NotEmpty().WithMessage("Chọn người phụ trách.");
        RuleFor(x => x.RemindBeforeDays).InclusiveBetween(0, 365).WithMessage("Nhắc trước từ 0 đến 365 ngày.");
    }
}

public sealed class MaintenanceListQuery
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public Guid? EquipmentId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed record MaintenanceOption(Guid Value, string Label);
public sealed record MaintenanceOptions(List<MaintenanceOption> Equipments, List<MaintenanceOption> Assignees);
public sealed record MaintenanceSummary(int Total, int Pending, int InProgress, int Overdue);
public sealed record MaintenanceList(PagedResponse<MaintenanceItem> Page, MaintenanceSummary Summary);

public sealed record MaintenanceItem(Guid Id, Guid EquipmentId, string EquipmentName, string? Location,
    string TaskName, string RepeatUnit, int RepeatEvery, DateOnly StartDate, Guid? AssignedTo,
    string AssigneeName, int RemindBeforeDays, string Status, bool IsOverdue, DateOnly? NextDate);

public sealed class CompleteMaintenanceRequest
{
    public decimal Cost { get; set; }
    public string? VendorName { get; set; }
    public string Note { get; set; } = string.Empty;
    public bool ReadyForUse { get; set; }
}

public sealed record MaintenanceHistoryItem(Guid Id, Guid? ScheduleId, Guid EquipmentId, string EquipmentName,
    string TaskName, DateOnly? PlannedDate, DateTime ActualDate, decimal Cost, string PerformedBy,
    string? VendorName, string? Note);

public sealed class MaintenanceException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

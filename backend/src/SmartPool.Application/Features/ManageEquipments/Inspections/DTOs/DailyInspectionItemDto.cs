using System;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

public sealed class DailyInspectionItemDto
{
    public Guid EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsPassed { get; set; } = true;
    public string? Note { get; set; }

    /// <summary>
    /// Mức độ ưu tiên theo luật an toàn: "Critical" (Máy bơm, Phao, Sào), "High" (Loa, Tủ thuốc), "Normal" (Ghế, Dù...)
    /// </summary>
    public string PriorityLevel { get; set; } = "Normal";

    /// <summary>
    /// Danh sách ảnh hiện trạng hư hỏng do nhân viên kiểm tra chụp
    /// </summary>
    public List<string> DefectImageUrls { get; set; } = [];

    /// <summary>
    /// Trạng thái xử lý sự cố: "Pending" (Chờ xử lý), "Repaired" (Đã sửa chữa), "Replaced" (Đã thay thế).
    /// Null nếu IsPassed == true.
    /// </summary>
    public string? ResolutionStatus { get; set; }

    /// <summary>
    /// Thời gian Admin xác nhận xử lý xong
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Tên người xác nhận xử lý (Admin)
    /// </summary>
    public string? ResolvedBy { get; set; }

    /// <summary>
    /// Ghi chú phương án xử lý
    /// </summary>
    public string? ResolutionNote { get; set; }

    /// <summary>
    /// Danh sách ảnh minh chứng sau khi sửa chữa / thay thế do Admin cung cấp
    /// </summary>
    public List<string> ResolutionImageUrls { get; set; } = [];
}

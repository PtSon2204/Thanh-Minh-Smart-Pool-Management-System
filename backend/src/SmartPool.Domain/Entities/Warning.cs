using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartPool.Domain.Entities
{
    //sonpt thêm bảng warning - 10.10.2026
    public class Warning
    {
        public Guid Id { get; set; }
        public Guid? CamId { get; set; }
        public DateTime WarningTime { get; set; }
        // Bản ghi mới phải có URL ảnh đã upload; giá trị rỗng chỉ còn ở dữ liệu cũ.
        public string ImageUrl { get; set; } = "";
        public string SeverityLevel { get; set; } = null!;
        public decimal Confidence { get; set; }
        public string Status { get; set; } = "Pending";
        public Guid? IncidentId { get; set; }
        public Guid? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Giám sát an toàn — chuỗi nhận diện một "risk episode" dùng để gộp cảnh báo trùng lặp
        public string? RiskEpisode { get; set; }
        // Định danh track của đối tượng bị theo dõi trong video
        public string? TrackId { get; set; }
        // Trạng thái upload ảnh snapshot lên Cloudinary
        public string SnapshotStatus { get; set; } = "NONE";
        public int SnapshotAttempts { get; set; }
        // Khung cảnh báo (x1,y1,x2,y2) chuẩn hóa 0..1 theo khung hình — dùng vẽ overlay LIVE.
        public double[]? Box { get; set; }

        // Navigation properties
        public virtual Equipment? Cam { get; set; }
        public virtual Incident? Incident { get; set; }
        public virtual User? Verifier { get; set; }
    }
}

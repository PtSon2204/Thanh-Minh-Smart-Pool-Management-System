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
        // null khi cảnh báo đến từ video tải lên (UPLOAD), không gắn camera
        public Guid? CamId { get; set; }
        public DateTime WarningTime { get; set; }
        // Rỗng cho tới khi upload xong ảnh snapshot lên Cloudinary; không bao giờ null (cột NOT NULL).
        public string ImageUrl { get; set; } = "";
        public string SeverityLevel { get; set; } = null!;
        public decimal Confidence { get; set; }
        public string Status { get; set; } = "Pending";
        public Guid? IncidentId { get; set; }
        public Guid? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Giám sát an toàn — nguồn cảnh báo (LIVE từ camera, UPLOAD từ video tải lên)
        public string SourceType { get; set; } = "LIVE";
        // Chuỗi nhận diện một "risk episode" dùng để gộp cảnh báo trùng lặp
        public string? RiskEpisode { get; set; }
        // Định danh track của đối tượng bị theo dõi trong video
        public string? TrackId { get; set; }
        // Job phân tích video sinh ra cảnh báo (null với cảnh báo LIVE)
        public Guid? VideoJobId { get; set; }
        // Vị trí (giây) của khung hình gây cảnh báo trong video
        public double? VideoTimestamp { get; set; }
        // Trạng thái upload ảnh snapshot lên Cloudinary
        public string SnapshotStatus { get; set; } = "NONE";
        public int SnapshotAttempts { get; set; }

        // Navigation properties
        public virtual Equipment? Cam { get; set; }
        public virtual Incident? Incident { get; set; }
        public virtual User? Verifier { get; set; }
        public virtual VideoAnalysisJob? VideoJob { get; set; }
    }
}

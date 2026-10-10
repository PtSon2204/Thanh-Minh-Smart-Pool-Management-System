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
        public Guid CamId { get; set; }
        public DateTime WarningTime { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string SeverityLevel { get; set; } = null!;
        public decimal Confidence { get; set; }
        public string Status { get; set; } = "Pending";
        public Guid? IncidentId { get; set; }
        public Guid? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public virtual Equipment Cam { get; set; }
        public virtual Incident? Incident { get; set; }
        public virtual User? Verifier { get; set; }
    }
}

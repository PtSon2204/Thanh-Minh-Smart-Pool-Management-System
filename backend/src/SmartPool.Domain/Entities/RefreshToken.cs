using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartPool.Domain.Entities
{
    //sonpt thêm bảng refreshToken - 10.10.2026
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTime ExpiredAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RevokeAt { get; set; }
        // Navigation property
        public virtual User User { get; set; }
    }
}

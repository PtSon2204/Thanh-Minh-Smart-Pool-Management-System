using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes
{
    /// <summary>
    /// DTO trả về cho mỗi loại vé trong danh sách.
    /// </summary>
    public class GetAllTicketTypesResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Enum phân loại: VE_THANG | VE_LUOT | VE_THUONG.</summary>
        public TicketCategoryEnum TicketCategory { get; set; }

        public decimal Price { get; set; }

        /// <summary>Số ngày hiệu lực. Null = không giới hạn.</summary>
        public int? DurationDays { get; set; }

        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}

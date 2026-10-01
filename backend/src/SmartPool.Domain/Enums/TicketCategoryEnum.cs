using System.Text.Json.Serialization;

namespace SmartPool.Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TicketCategoryEnum
    {
        /// <summary>Vé tháng - 1 lượt/ngày, có QR, mua cả online lẫn tại quầy</summary>
        VE_THANG,
        /// <summary>Vé lượt - mua tại quầy: trừ lượt ngay, không QR. Mua online: có QR, hết hạn cuối ngày</summary>
        VE_LUOT,
    }
}

using System.Text.Json.Serialization;

namespace SmartPool.Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TicketCategoryEnum
    {
        VE_THANG,
        VE_LUOT,
        VE_THUONG,
    }
}

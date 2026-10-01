using System.Text.Json.Serialization;

namespace SmartPool.Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TicketStatusEnum
    {
        ACTIVE,
        EXPIRED,
        USED,
        CANCELLED
    }
}

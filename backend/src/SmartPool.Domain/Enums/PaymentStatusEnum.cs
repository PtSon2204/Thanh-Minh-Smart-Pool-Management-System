using System.Text.Json.Serialization;

namespace SmartPool.Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentStatusEnum
    {
        PENDING,
        COMPLETED,
        PARTIAL,
        FAILED,
        REFUNDED
    }
}

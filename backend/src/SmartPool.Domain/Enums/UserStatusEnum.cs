using System.Text.Json.Serialization;

namespace SmartPool.Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserStatusEnum
    {
        ACTIVE,
        INACTIVE,
        LOCKED
    }
}

using System.Text.Json.Serialization;

namespace SmartPool.Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TicketCategoryEnum
    {
        //Vé tháng
        VE_THANG,
        //Vé dùng tùy vào số lượt trên vé (15-20 lượt)
        VE_LUOT,
        //Vé dùng 1 lần vào bể
        VE_THUONG,
    }
}

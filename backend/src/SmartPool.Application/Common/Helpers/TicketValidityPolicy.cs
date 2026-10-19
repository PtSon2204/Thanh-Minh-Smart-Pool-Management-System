using SmartPool.Domain.Enums;

namespace SmartPool.Application.Common.Helpers;

public static class TicketValidityPolicy
{
    public static int GetDurationDays(TicketCategoryEnum category) => category switch
    {
        TicketCategoryEnum.VE_THANG => 30,
        TicketCategoryEnum.VE_LUOT => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(category), "Phân loại vé không hợp lệ.")
    };

    public static DateTime GetExpiryDate(DateTime purchasedAt, string? category) => category switch
    {
        nameof(TicketCategoryEnum.VE_THANG) => purchasedAt.AddDays(30),
        nameof(TicketCategoryEnum.VE_LUOT) => purchasedAt.AddHours(24),
        _ => throw new ArgumentOutOfRangeException(nameof(category), "Phân loại vé không hợp lệ.")
    };
}

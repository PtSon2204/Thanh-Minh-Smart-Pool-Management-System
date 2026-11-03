using SmartPool.Domain.Enums;

namespace SmartPool.Application.Common.Helpers;

public static class TicketValidityPolicy
{
    public static int? GetDurationDays(TicketCategoryEnum category, int? totalEntries = null) => category switch
    {
        TicketCategoryEnum.VE_THANG => 30,
        TicketCategoryEnum.VE_LUOT => (totalEntries.HasValue && totalEntries.Value > 1) ? null : 1,
        _ => throw new ArgumentOutOfRangeException(nameof(category), "Phân loại vé không hợp lệ.")
    };

    public static DateTime? GetExpiryDate(DateTime purchasedAt, string? category, int? totalEntries = null) => category switch
    {
        nameof(TicketCategoryEnum.VE_THANG) => purchasedAt.AddDays(30),
        nameof(TicketCategoryEnum.VE_LUOT) => (totalEntries.HasValue && totalEntries.Value > 1) ? null : purchasedAt.AddHours(24),
        _ => throw new ArgumentOutOfRangeException(nameof(category), "Phân loại vé không hợp lệ.")
    };
}

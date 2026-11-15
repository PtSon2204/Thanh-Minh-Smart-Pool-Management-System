using SmartPool.Domain.Enums;

namespace SmartPool.Application.Common.Helpers;

public static class TicketValidityPolicy
{
    public static int? GetDurationDays(TicketCategoryEnum category, int? totalEntries = null)
    {
        switch (category)
        {
            case TicketCategoryEnum.VE_THANG:
                return 30;

            case TicketCategoryEnum.VE_LUOT:
                return (totalEntries.HasValue && totalEntries.Value > 1) ? null : 1;

            default:
                throw new ArgumentOutOfRangeException(nameof(category), "Phân loại vé không hợp lệ.");
        }
    }

    public static DateTime? GetExpiryDate(DateTime purchasedAt, string? category, int? totalEntries = null)
    {
        switch (category)
        {
            case nameof(TicketCategoryEnum.VE_THANG):
                return purchasedAt.AddDays(30);

            case nameof(TicketCategoryEnum.VE_LUOT):
                return (totalEntries.HasValue && totalEntries.Value > 1) ? null : purchasedAt.AddHours(24);
                 
            default:
                throw new ArgumentOutOfRangeException(nameof(category), "Phân loại vé không hợp lệ.");
        }
    }
}

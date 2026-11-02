using SmartPool.Domain.Entities;

namespace SmartPool.Application.Common.Helpers;

public static class OnlineOrderPaymentPolicy
{
    //public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    public static bool IsCheckoutReference(string? reference) =>
        reference is { Length: 8 } && reference.StartsWith("SP", StringComparison.OrdinalIgnoreCase);

    public static bool IsOnline(Payment? payment) =>
        string.Equals(payment?.PaymentMethod, "TRANSFER", StringComparison.OrdinalIgnoreCase);

    public static DateTime? GetExpiresAtUtc(Order order, Payment? payment) =>
        IsOnline(payment) && order.CreatedAt.HasValue
            ? DateTime.SpecifyKind(order.CreatedAt.Value, DateTimeKind.Utc).Add(Lifetime)
            : null;

    public static bool TryExpire(Order order, Payment? payment, DateTime nowUtc)
    {
        if (order.Status is not ("PENDING" or "PARTIAL") ||
            GetExpiresAtUtc(order, payment) is not { } deadline || nowUtc < deadline)
            return false;

        order.Status = "CANCELLED";
        order.UpdatedAt = nowUtc;
        if (payment?.Status == "PENDING") payment.Status = "FAILED";
        return true;
    }
}

using SmartPool.Domain.Entities;

namespace SmartPool.Application.Common.Helpers;

public static class OnlineOrderPaymentPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public static bool IsCheckoutReference(string? reference) =>
        reference is { Length: 8 } && reference.StartsWith("SP", StringComparison.OrdinalIgnoreCase);

    public static bool IsOnline(Payment? payment) =>
        payment?.PaymentMethod is not null &&
        (payment.PaymentMethod.Equals("TRANSFER", StringComparison.OrdinalIgnoreCase) ||
         payment.PaymentMethod.Equals("BANK_TRANSFER", StringComparison.OrdinalIgnoreCase));

    public static DateTime? GetExpiresAtUtc(Order order, Payment? payment)
    {
        var isOnline = IsOnline(payment) || (order.Payments != null && order.Payments.Any(IsOnline));
        return isOnline && order.CreatedAt.HasValue
            ? DateTime.SpecifyKind(order.CreatedAt.Value, DateTimeKind.Utc).Add(Lifetime)
            : null;
    }

    public static bool TryExpire(Order order, Payment? payment, DateTime nowUtc)
    {
        var orderStatus = order.Status?.Trim().ToUpperInvariant();
        if (orderStatus is not ("PENDING" or "PARTIAL") ||
            GetExpiresAtUtc(order, payment) is not { } deadline || nowUtc < deadline)
            return false;

        order.Status = "CANCELLED";
        order.UpdatedAt = nowUtc;
        if (payment != null && string.Equals(payment.Status?.Trim(), "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = "FAILED";
        }
        if (order.Payments != null)
        {
            foreach (var p in order.Payments)
            {
                if (string.Equals(p.Status?.Trim(), "PENDING", StringComparison.OrdinalIgnoreCase))
                {
                    p.Status = "FAILED";
                }
            }
        }
        return true;
    }
}

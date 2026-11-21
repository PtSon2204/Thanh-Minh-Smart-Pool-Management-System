using System;
using SmartPool.Application.Common.Helpers;
using SmartPool.Domain.Entities;
using Xunit;

namespace SmartPool.UnitTests.Features.ManagePayments;

public sealed class OnlineOrderPaymentPolicyTests
{
    [Fact(DisplayName = "TryExpire - Đơn hàng quá 5 phút -> Chuyển Status sang CANCELLED và Payment sang FAILED")]
    public void TryExpire_WhenPast5Minutes_CancelsOrderAndFailsPayment()
    {
        var now = DateTime.UtcNow;
        var createdAt = now.AddMinutes(-6);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = "PENDING",
            CreatedAt = createdAt
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PaymentMethod = "BANK_TRANSFER",
            Status = "PENDING",
            CreatedAt = createdAt
        };

        var expired = OnlineOrderPaymentPolicy.TryExpire(order, payment, now);

        Assert.True(expired);
        Assert.Equal("CANCELLED", order.Status);
        Assert.Equal("FAILED", payment.Status);
        Assert.Equal(now, order.UpdatedAt);
    }

    [Fact(DisplayName = "TryExpire - Đơn hàng trạng thái chữ thường (Pending) -> Vẫn expire thành công (Case-insensitive)")]
    public void TryExpire_WhenStatusIsMixedCase_CancelsOrderAndFailsPayment()
    {
        var now = DateTime.UtcNow;
        var createdAt = now.AddMinutes(-10);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = "Pending",
            CreatedAt = createdAt
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PaymentMethod = "TRANSFER",
            Status = "Pending",
            CreatedAt = createdAt
        };

        var expired = OnlineOrderPaymentPolicy.TryExpire(order, payment, now);

        Assert.True(expired);
        Assert.Equal("CANCELLED", order.Status);
        Assert.Equal("FAILED", payment.Status);
    }

    [Fact(DisplayName = "TryExpire - Đơn hàng chưa quá 5 phút -> Không expire, giữ nguyên trạng thái")]
    public void TryExpire_WhenWithin5Minutes_DoesNotExpire()
    {
        var now = DateTime.UtcNow;
        var createdAt = now.AddMinutes(-2);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = "PENDING",
            CreatedAt = createdAt
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PaymentMethod = "BANK_TRANSFER",
            Status = "PENDING",
            CreatedAt = createdAt
        };

        var expired = OnlineOrderPaymentPolicy.TryExpire(order, payment, now);

        Assert.False(expired);
        Assert.Equal("PENDING", order.Status);
        Assert.Equal("PENDING", payment.Status);
    }

    [Fact(DisplayName = "TryExpire - Đơn hàng đã COMPLETED -> Không thể expire")]
    public void TryExpire_WhenAlreadyCompleted_DoesNotExpire()
    {
        var now = DateTime.UtcNow;
        var createdAt = now.AddMinutes(-15);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = "COMPLETED",
            CreatedAt = createdAt
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PaymentMethod = "BANK_TRANSFER",
            Status = "COMPLETED",
            CreatedAt = createdAt
        };

        var expired = OnlineOrderPaymentPolicy.TryExpire(order, payment, now);

        Assert.False(expired);
        Assert.Equal("COMPLETED", order.Status);
    }
}

using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<RegisterForSwimClassResponse> RegisterAsync(
        Guid classId,
        Guid customerUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(classId, cancellationToken);

        var swimClass = await context.SwimClasses
            .SingleOrDefaultAsync(item => item.Id == classId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (swimClass.Status != "Active" ||
            now < swimClass.RegistrationStartAt || now >= swimClass.RegistrationEndAt)
            throw new SwimClassStateConflictException("Lớp không trong thời gian nhận đăng ký.");

        var existing = await context.ClassRegistrations
            .Include(item => item.Order)
            .SingleOrDefaultAsync(item => item.ClassId == classId &&
                item.CustomerUserId == customerUserId, cancellationToken);
        if (existing?.Status == "Confirmed")
            throw new SwimClassStateConflictException("Bạn đã đăng ký lớp này.");

        if (existing?.Status == "PendingPayment" && existing.Order is { } pendingOrder)
        {
            var originalPayment = await context.Payments
                .Where(item => item.OrderId == pendingOrder.Id && item.PaymentMethod == "TRANSFER"
                    && item.TransactionRef != null && item.TransactionRef.StartsWith("SP")
                    && item.TransactionRef.Length == 8)
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (pendingOrder.Status is "PENDING" or "PARTIAL" &&
                originalPayment != null &&
                OnlineOrderPaymentPolicy.GetExpiresAtUtc(pendingOrder, originalPayment) > now)
            {
                await transaction.CommitAsync(cancellationToken);
                return new RegisterForSwimClassResponse(existing.Id, classId, existing.Status,
                    pendingOrder.Id, pendingOrder.Status!, existing.Amount,
                    originalPayment.TransactionRef!,
                    OnlineOrderPaymentPolicy.GetExpiresAtUtc(pendingOrder, originalPayment), now, true);
            }

            if (pendingOrder.Status is "PENDING" or "PARTIAL")
            {
                pendingOrder.Status = "CANCELLED";
                pendingOrder.UpdatedAt = now;
                if (originalPayment?.Status == "PENDING") originalPayment.Status = "FAILED";
            }
            else if (pendingOrder.Status == "COMPLETED")
                throw new SwimClassStateConflictException(
                    "Đơn thanh toán của lớp đã hoàn tất. Vui lòng kiểm tra trạng thái đăng ký.");

            existing.Status = "Cancelled";
            existing.CancelledAt = now;
            existing.UpdatedAt = now;
        }
        else if (existing is { Status: not "Cancelled" })
            throw new SwimClassStateConflictException("Đăng ký lớp hiện tại chưa thể tạo lại.");

        var occupied = await context.ClassRegistrations.AsNoTracking()
            .CountAsync(item => item.ClassId == classId &&
                (item.Status == "Confirmed" ||
                    (item.Status == "PendingPayment" && item.Order != null &&
                     (item.Order.Status == "COMPLETED" ||
                      ((item.Order.Status == "PENDING" || item.Order.Status == "PARTIAL") &&
                       item.Order.CreatedAt > now.Subtract(OnlineOrderPaymentPolicy.Lifetime))))),
                cancellationToken);
        if (occupied >= swimClass.Capacity)
            throw new SwimClassStateConflictException("Lớp đã hết chỗ đăng ký.");

        var transactionRef = await NewPaymentReferenceAsync(cancellationToken);
        var orderId = Guid.NewGuid();
        var isFree = swimClass.Fee == 0;
        var order = new Order
        {
            Id = orderId,
            UserId = customerUserId,
            TotalAmount = swimClass.Fee,
            DiscountAmount = 0,
            FinalAmount = swimClass.Fee,
            Status = isFree ? "COMPLETED" : "PENDING",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = swimClass.Fee,
            PaymentMethod = "TRANSFER",
            TransactionRef = transactionRef,
            Status = isFree ? "COMPLETED" : "PENDING",
            PaymentTime = isFree ? now : null,
            CreatedAt = now
        };
        context.Orders.Add(order);
        context.OrderDetails.Add(new OrderDetail
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ItemType = "SWIM_CLASS",
            ItemId = classId,
            Quantity = 1,
            UnitPrice = swimClass.Fee,
            CreatedAt = now
        });
        context.Payments.Add(payment);

        var registration = existing ?? new ClassRegistration
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            CustomerUserId = customerUserId,
            CreatedAt = now
        };
        registration.OrderId = orderId;
        registration.Order = order;
        registration.Amount = swimClass.Fee;
        registration.Status = isFree ? "Confirmed" : "PendingPayment";
        registration.RegisteredAt = now;
        registration.ConfirmedAt = isFree ? now : null;
        registration.CancelledAt = null;
        registration.UpdatedAt = now;
        if (existing is null) context.ClassRegistrations.Add(registration);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RegisterForSwimClassResponse(registration.Id, classId,
            registration.Status, orderId, order.Status!, swimClass.Fee,
            transactionRef,
            isFree ? null : OnlineOrderPaymentPolicy.GetExpiresAtUtc(order, payment), now);
    }

    private async Task<string> NewPaymentReferenceAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var reference = "SP" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            if (!await context.Payments.AsNoTracking().AnyAsync(
                    item => item.TransactionRef == reference, cancellationToken))
                return reference;
        }

        throw new InvalidOperationException("Không thể tạo mã thanh toán. Vui lòng thử lại.");
    }
}

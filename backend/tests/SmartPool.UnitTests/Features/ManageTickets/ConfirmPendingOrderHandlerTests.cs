using Microsoft.Extensions.Caching.Memory;
using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets;

public sealed class ConfirmPendingOrderHandlerTests
{
    [Theory]
    [InlineData("2026-10-05T16:59:59Z", "2026-10-06T16:59:59Z")]
    [InlineData("2026-10-05T17:00:00Z", "2026-10-06T17:00:00Z")]
    public async Task Handle_OnlineOrdinaryTicket_ExpiresExactly24HoursAfterPurchase(string fixedNow, string expectedExpiry)
    {
        var actual = await ConfirmTicketAsync(TicketCategoryEnum.VE_LUOT.ToString(),
            null, DateTime.Parse(fixedNow, null, System.Globalization.DateTimeStyles.RoundtripKind));

        Assert.Equal(DateTime.Parse(expectedExpiry, null, System.Globalization.DateTimeStyles.RoundtripKind), actual.ExpiryDate);
    }

    [Fact]
    public async Task Handle_FutureScheduledOrdinaryTicket_UsesPurchaseTime()
    {
        var effectiveStart = new DateTime(2026, 10, 7, 18, 30, 0, DateTimeKind.Utc);
        var ticket = await ConfirmTicketAsync(TicketCategoryEnum.VE_LUOT.ToString(), effectiveStart,
            new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc), ticket.ExpiryDate);
        Assert.Equal(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc), ticket.IssueDate);
    }

    [Fact]
    public async Task Handle_MonthlyTicket_IgnoresLegacyDurationAndUses30DaysFromPurchase()
    {
        var issueDate = new DateTime(2026, 10, 5, 16, 59, 59, DateTimeKind.Utc);
        var ticket = await ConfirmTicketAsync(TicketCategoryEnum.VE_THANG.ToString(), issueDate,
            new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc), durationDays: 45);

        var purchasedAt = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(purchasedAt, ticket.IssueDate);
        Assert.Equal(purchasedAt.AddDays(30), ticket.ExpiryDate);
    }

    [Fact]
    public async Task Handle_OrdinaryExpiryAtBoundary_IsDeniedBySharedExpiryRule()
    {
        var purchasedAt = new DateTime(2026, 10, 5, 17, 0, 0, DateTimeKind.Utc);
        var ticket = await ConfirmTicketAsync(TicketCategoryEnum.VE_LUOT.ToString(),
            null, purchasedAt);

        Assert.False(SmartPool.Application.Common.Time.VietnamTimeBoundary.HasNotExpired(
            ticket.ExpiryDate, purchasedAt.AddHours(24)));
        Assert.True(SmartPool.Application.Common.Time.VietnamTimeBoundary.HasNotExpired(
            ticket.ExpiryDate, purchasedAt.AddHours(24).AddTicks(-1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(30)]
    [InlineData(0)]
    public async Task Handle_SingleUseTicket_IgnoresLegacyDuration(int? durationDays)
    {
        var purchasedAt = new DateTime(2026, 10, 5, 16, 59, 59, DateTimeKind.Utc);
        var ticket = await ConfirmTicketAsync("VE_LUOT", null, purchasedAt, durationDays);
        Assert.Equal(purchasedAt.AddHours(24), ticket.ExpiryDate);
    }

    private static async Task<Ticket> ConfirmTicketAsync(string category, DateTime? effectiveIssueDate,
        DateTime fixedNow, int? durationDays = null)
    {
        var orderId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var payment = new Payment { OrderId = orderId, Amount = 100, TransactionRef = "test-ref", Status = "PENDING" };
        var order = new Order { Id = orderId };
        var detail = new OrderDetail { OrderId = orderId, ItemId = ticketTypeId, Quantity = 1 };
        var ticketType = new TicketTypeEntity { Id = ticketTypeId, TicketCategory = category, DurationDays = durationDays };
        Ticket? createdTicket = null;

        var paymentRepo = new Mock<IRepository<Payment>>();
        paymentRepo.Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Payment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var orderRepo = new Mock<IRepository<Order>>();
        orderRepo.Setup(repo => repo.GetByIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var detailRepo = new Mock<IRepository<OrderDetail>>();
        detailRepo.Setup(repo => repo.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<OrderDetail, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { detail });
        var ticketTypeRepo = new Mock<IRepository<TicketTypeEntity>>();
        ticketTypeRepo.Setup(repo => repo.GetByIdAsync(ticketTypeId, It.IsAny<CancellationToken>())).ReturnsAsync(ticketType);
        var ticketRepo = new Mock<IRepository<Ticket>>();
        ticketRepo.Setup(repo => repo.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((ticket, _) => createdTicket = ticket)
            .Returns(Task.CompletedTask);
        // GenerateUniqueCodeAsync: trả về null = mã chưa tồn tại → dùng ngay
        ticketRepo.Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Ticket, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        using var cache = new MemoryCache(new MemoryCacheOptions());
        if (effectiveIssueDate.HasValue)
        {
            cache.Set($"OrderStartDate_{orderId}", effectiveIssueDate.Value);
        }
        var handler = new ConfirmPendingOrderHandler(ticketTypeRepo.Object, orderRepo.Object, detailRepo.Object,
            paymentRepo.Object, ticketRepo.Object, unitOfWork.Object, cache, new FixedTimeProvider(fixedNow));

        var result = await handler.Handle(new ConfirmPendingOrderCommand
        {
            TransactionRef = "test-ref",
            ActualAmount = 100
        }, CancellationToken.None);

        Assert.True(result.Success);
        var ticket = Assert.IsType<Ticket>(createdTicket);
        Assert.Matches(@"^\d{6}$", ticket.QrCode);
        Assert.Equal("ACTIVE", ticket.Status);
        Assert.Single(result.Tickets);
        Assert.Equal(category, result.Tickets[0].TicketCategory);
        Assert.Equal(ticket.ExpiryDate, result.Tickets[0].ExpiryDate);
        return ticket;
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}

using System.Linq.Expressions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.CheckoutOnline;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.CreatePendingOrder;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageVouchers;

public class VoucherCheckoutValidityTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 5, 0, 0, DateTimeKind.Utc);

    public static IEnumerable<object[]> InvalidCheckouts =>
        from checkout in new[] { "online", "cash", "transfer" }
        from state in new[] { "expired", "at-expiry", "scheduled", "locked", "deleted" }
        select new object[] { checkout, state };

    [Theory]
    [MemberData(nameof(InvalidCheckouts))]
    public async Task InvalidVoucher_IsRejectedBeforeSavingOrCreatingAnOrder(string checkout, string state)
    {
        var harness = new CheckoutHarness(CreateVoucher(state));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Checkout(checkout));

        Assert.Contains("không hợp lệ hoặc đã hết hạn", error.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Payments.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("online")]
    [InlineData("cash")]
    [InlineData("transfer")]
    public async Task VoucherStartingNow_IsAppliedUsingServerTimeAndNormalizedCode(string checkout)
    {
        var voucher = CreateVoucher("active");
        voucher.StartDate = Now;
        var harness = new CheckoutHarness(voucher);

        await harness.Checkout(checkout);

        harness.Orders.Verify(r => r.AddAsync(It.Is<Order>(o => o.VoucherId == voucher.Id
            && o.TotalAmount == 100000 && o.DiscountAmount == 20000 && o.FinalAmount == 80000),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("online")]
    [InlineData("cash")]
    [InlineData("transfer")]
    public async Task VoucherExpiringAfterValidation_IsRejectedOnTheNextCheckout(string checkout)
    {
        var harness = new CheckoutHarness(CreateVoucher("active"));
        await harness.Checkout(checkout);
        harness.Clock.Now = Now.AddMinutes(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Checkout(checkout));

        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Voucher CreateVoucher(string state) => new()
    {
        Id = Guid.NewGuid(), Code = "GIAM20K", DiscountType = "FIXED_AMOUNT", DiscountValue = 20000,
        IsActive = state != "locked", IsDeleted = state == "deleted",
        StartDate = state == "scheduled" ? Now.AddSeconds(1) : Now.AddDays(-1),
        EndDate = state switch
        {
            "expired" => new DateTime(2026, 10, 9, 17, 0, 0, DateTimeKind.Utc),
            "at-expiry" => Now,
            _ => Now.AddMinutes(1)
        }
    };

    private sealed class CheckoutHarness
    {
        private readonly Mock<IRepository<TicketType>> ticketTypes = new();
        private readonly Mock<IRepository<OrderDetail>> details = new();
        private readonly Mock<IRepository<Ticket>> tickets = new();
        private readonly Mock<IRepository<User>> users = new();
        private readonly Mock<IRepository<Voucher>> vouchers = new();
        public Mock<IRepository<Order>> Orders { get; } = new();
        public Mock<IRepository<Payment>> Payments { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public MutableTimeProvider Clock { get; } = new();
        private readonly Guid ticketTypeId = Guid.NewGuid();

        public CheckoutHarness(Voucher voucher)
        {
            ticketTypes.Setup(r => r.GetByIdAsync(ticketTypeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TicketType { Id = ticketTypeId, Name = "Vé lượt", Price = 100000,
                    IsActive = true, TicketCategory = "VE_LUOT" });
            // Evaluate the production predicate, so ignoring dates/locks/deletion fails the tests.
            vouchers.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Voucher, bool>> predicate, CancellationToken _) =>
                    Task.FromResult(predicate.Compile()(voucher) ? voucher : null));
        }

        public async Task Checkout(string checkout)
        {
            const string code = "  giam20k  ";
            using var cache = new MemoryCache(new MemoryCacheOptions());
            switch (checkout)
            {
                case "online":
                    await new CheckoutOnlineHandler(ticketTypes.Object, Orders.Object, details.Object,
                        Payments.Object, vouchers.Object, UnitOfWork.Object, Clock, Mock.Of<ISender>())
                        .Handle(new() { VoucherCode = code, Items = [new() { TicketTypeId = ticketTypeId, Quantity = 1 }] }, default);
                    break;
                case "cash":
                    await new SellOfflineTicketHandler(tickets.Object, ticketTypes.Object, Orders.Object,
                        details.Object, users.Object, Payments.Object, vouchers.Object, UnitOfWork.Object, Clock)
                        .Handle(new() { VoucherCode = code, Items = [new() { TicketTypeId = ticketTypeId, Quantity = 1 }] }, default);
                    break;
                case "transfer":
                    await new CreatePendingOrderHandler(ticketTypes.Object, Orders.Object, details.Object,
                        Payments.Object, users.Object, vouchers.Object, UnitOfWork.Object, cache, Clock)
                        .Handle(new() { VoucherCode = code, Items = [new() { TicketTypeId = ticketTypeId, Quantity = 1 }] }, default);
                    break;
                default: throw new ArgumentException(checkout);
            }
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTime Now { get; set; } = VoucherCheckoutValidityTests.Now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}

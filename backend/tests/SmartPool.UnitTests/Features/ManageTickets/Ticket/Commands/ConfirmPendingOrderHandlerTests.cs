using System.Linq.Expressions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;
using PaymentEntity = SmartPool.Domain.Entities.Payment;
using OrderEntity = SmartPool.Domain.Entities.Order;
using OrderDetailEntity = SmartPool.Domain.Entities.OrderDetail;
using TicketEntity = SmartPool.Domain.Entities.Ticket;

namespace SmartPool.UnitTests.Features.ManageTickets.Ticket.Commands;

public sealed class ConfirmPendingOrderHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 5, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "UTCID01 (A) - TransactionRef không tồn tại -> Trả thất bại và không khóa đơn")]
    public async Task UTCID01_TransactionReferenceMissing_ReturnsFailureWithoutLock()
    {
        var h = new Harness();
        var result = await h.Handle(new() { TransactionRef = "SP123456", ActualAmount = 100 });

        Assert.False(result.Success);
        Assert.Equal("TransactionRef not found.", result.Message);
        h.Operations.Verify(o => o.ExecuteLockedAsync(It.IsAny<Guid>(), It.IsAny<Func<OrderEntity?, CancellationToken, Task<ConfirmPendingOrderResponse>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID02 (A) - Order không tồn tại khi khóa -> Trả thất bại")]
    public async Task UTCID02_OrderMissing_ReturnsFailure()
    {
        var h = new Harness(missingOrder: true);
        h.Payments.Add(h.PendingPayment);

        var result = await h.Handle(h.Command(amount: 100));

        Assert.False(result.Success);
        Assert.Equal("Order not found.", result.Message);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID03 (A) - Không đọc lại được payment trong lock -> Trả TransactionRef not found")]
    public async Task UTCID03_PaymentMissingAfterLock_ReturnsFailure()
    {
        var h = new Harness();
        h.Payments.Add(h.PendingPayment);
        h.PaymentRepo.SetupSequence(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(h.PendingPayment).ReturnsAsync((PaymentEntity?)null);

        var result = await h.Handle(h.Command(amount: 100));

        Assert.False(result.Success);
        Assert.Equal("TransactionRef not found.", result.Message);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID04 (A) - ActualAmount âm -> Trả lỗi số tiền và không lưu")]
    public async Task UTCID04_NegativeIncomingAmount_IsRejected()
    {
        var h = new Harness(); h.Payments.Add(h.PendingPayment);

        var result = await h.Handle(h.Command(amount: -1));

        Assert.False(result.Success);
        Assert.Equal("Invalid incoming amount.", result.Message);
        h.PaymentRepo.Verify(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 (B) - ActualAmount bằng 0 khi FinalAmount lớn hơn 0 -> Trả lỗi số tiền")]
    public async Task UTCID05_ZeroIncomingAmountForPositiveDue_IsRejected()
    {
        var h = new Harness(); h.Payments.Add(h.PendingPayment);

        var result = await h.Handle(h.Command(amount: 0));

        Assert.False(result.Success);
        Assert.Equal("Invalid incoming amount.", result.Message);
        h.PaymentRepo.Verify(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID06 (N) - BankReferenceCode đã xử lý -> Trả idempotent, không ghi nhận lặp")]
    public async Task UTCID06_DuplicateBankReference_ReturnsIdempotently()
    {
        var h = new Harness();
        h.Payments.AddRange([h.PendingPayment, new PaymentEntity { Id = Guid.NewGuid(), OrderId = h.Order.Id, TransactionRef = "BANK-1", Amount = 100, Status = "COMPLETED" }]);

        var result = await h.Handle(h.Command(amount: 100, bankReference: "BANK-1"));

        Assert.True(result.Success);
        Assert.Equal("Already processed (idempotent).", result.Message);
        Assert.Equal(h.Order.Id, result.OrderId);
        h.PaymentRepo.Verify(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID07 (N) - Tiền nhận thấp hơn số phải trả -> Đơn PARTIAL và lưu khoản nhận")]
    public async Task UTCID07_Underpayment_MarksPartialAndPersistsReceipt()
    {
        var h = new Harness(); h.Payments.Add(h.PendingPayment);

        var result = await h.Handle(h.Command(amount: 40, bankReference: "BANK-UNDER"));

        Assert.True(result.Success);
        Assert.Equal("PARTIAL", h.Order.Status);
        Assert.Equal("Underpayment. Expected: 100, Total received so far: 40, Remaining: 60", result.Message);
        var receipt = Assert.Single(h.AddedPayments);
        Assert.Equal(40, receipt.Amount);
        Assert.Equal("BANK-UNDER", receipt.TransactionRef);
        Assert.Equal("COMPLETED", receipt.Status);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID08 (N) - Nhận đủ tiền -> Hoàn tất đơn, cập nhật payment và phát hành vé")]
    public async Task UTCID08_FullPayment_CompletesOrderAndIssuesTickets()
    {
        var userId = Guid.NewGuid();
        var ticketType = new TicketTypeEntity { Id = Guid.NewGuid(), TicketCategory = nameof(TicketCategoryEnum.VE_LUOT) };
        var detail = new OrderDetailEntity { Id = Guid.NewGuid(), OrderId = Guid.NewGuid(), ItemId = ticketType.Id, Quantity = 1 };
        var h = new Harness(userId: userId, details: [detail], ticketTypes: [ticketType]);
        detail.OrderId = h.Order.Id;
        h.Payments.Add(h.PendingPayment);

        var result = await h.Handle(h.Command(amount: 100, bankReference: "BANK-FULL"));

        Assert.True(result.Success);
        Assert.Equal("COMPLETED", h.Order.Status);
        Assert.Equal("Confirmed and tickets generated.", result.Message);
        Assert.Equal("COMPLETED", h.PendingPayment.Status);
        h.Operations.Verify(o => o.UpdatePayment(h.PendingPayment), Times.Once);
        var receipt = Assert.Single(h.AddedPayments);
        Assert.Equal("BANK-FULL", receipt.TransactionRef);
        var issued = Assert.Single(h.AddedTickets);
        Assert.Equal(ticketType.Id, issued.TicketTypeId);
        Assert.Equal(userId, issued.UserId);
        Assert.Matches("^[0-9]{6}$", issued.QrCode);
        Assert.Equal(Now, issued.IssueDate);
        Assert.Equal(Now.AddHours(24), issued.ExpiryDate);
        Assert.Single(result.Tickets);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID09 (N) - Đơn đã hoàn tất nhưng nhận thêm tiền -> Ghi nhận đối soát, không phát hành thêm vé")]
    public async Task UTCID09_CompletedOrder_RecordsLateReceiptWithoutIssuingTickets()
    {
        var h = new Harness(orderStatus: "COMPLETED"); h.Payments.Add(h.PendingPayment);

        var result = await h.Handle(h.Command(amount: 10, bankReference: "BANK-LATE"));

        Assert.True(result.Success);
        Assert.Equal("Order already completed. Incoming payment recorded.", result.Message);
        Assert.Single(h.AddedPayments);
        Assert.Empty(h.AddedTickets);
        Assert.Empty(result.Tickets);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID10 (N) - Đơn đã hủy nhưng nhận tiền -> Lưu đối soát, không phát hành vé")]
    public async Task UTCID10_CancelledOrder_RecordsLateReceiptWithoutIssuingTickets()
    {
        var h = new Harness(orderStatus: "CANCELLED"); h.Payments.Add(h.PendingPayment);

        var result = await h.Handle(h.Command(amount: 10, bankReference: "BANK-CANCELLED"));

        Assert.True(result.Success);
        Assert.Equal("Order cancelled. Incoming payment recorded for reconciliation; no tickets issued.", result.Message);
        Assert.Single(h.AddedPayments);
        Assert.Empty(h.AddedTickets);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Harness
    {
        public readonly OrderEntity Order;
        public readonly PaymentEntity PendingPayment;
        public readonly List<PaymentEntity> Payments = [];
        public readonly List<PaymentEntity> AddedPayments = [];
        public readonly List<TicketEntity> AddedTickets = [];
        public readonly Mock<IRepository<PaymentEntity>> PaymentRepo = new();
        public readonly Mock<IUnitOfWork> UnitOfWork = new();
        public readonly Mock<IOrderPaymentOperations> Operations = new();
        private readonly Mock<IRepository<TicketTypeEntity>> _ticketTypes = new();
        private readonly Mock<IRepository<OrderDetailEntity>> _details = new();
        private readonly Mock<IRepository<TicketEntity>> _tickets = new();
        private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private readonly TimeProvider _time = new FixedTimeProvider(Now);

        private readonly bool _missingOrder;

        public Harness(OrderEntity? order = null, Guid? userId = null,
            IEnumerable<OrderDetailEntity>? details = null, IEnumerable<TicketTypeEntity>? ticketTypes = null,
            string orderStatus = "PENDING", bool missingOrder = false)
        {
            _missingOrder = missingOrder;
            Order = order ?? new OrderEntity { Id = Guid.NewGuid(), UserId = userId ?? Guid.NewGuid(), FinalAmount = 100, Status = orderStatus, CreatedAt = Now };
            PendingPayment = new PaymentEntity { Id = Guid.NewGuid(), OrderId = Order.Id, TransactionRef = "SP123456", PaymentMethod = "TRANSFER", Amount = Order.FinalAmount, Status = "PENDING", CreatedAt = Now };
            var detailRows = (details ?? []).ToArray();
            var typeRows = (ticketTypes ?? []).ToArray();

            PaymentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentEntity, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<PaymentEntity, bool>> predicate, CancellationToken _) => Task.FromResult(Payments.FirstOrDefault(predicate.Compile())));
            PaymentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<PaymentEntity, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<PaymentEntity, bool>> predicate, CancellationToken _) => Task.FromResult<IEnumerable<PaymentEntity>>(Payments.Where(predicate.Compile()).ToArray()));
            PaymentRepo.Setup(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
                .Callback<PaymentEntity, CancellationToken>((p, _) => AddedPayments.Add(p)).Returns(Task.CompletedTask);
            UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Operations.Setup(o => o.ExecuteLockedAsync(It.IsAny<Guid>(), It.IsAny<Func<OrderEntity?, CancellationToken, Task<ConfirmPendingOrderResponse>>>(), It.IsAny<CancellationToken>()))
                .Returns((Guid _, Func<OrderEntity?, CancellationToken, Task<ConfirmPendingOrderResponse>> action, CancellationToken token) => action(_missingOrder ? null : Order, token));
            _details.Setup(r => r.FindAsync(It.IsAny<Expression<Func<OrderDetailEntity, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<OrderDetailEntity, bool>> predicate, CancellationToken _) => Task.FromResult<IEnumerable<OrderDetailEntity>>(detailRows.Where(predicate.Compile()).ToArray()));
            _ticketTypes.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, CancellationToken _) => Task.FromResult(typeRows.FirstOrDefault(t => t.Id == id)));
            _tickets.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TicketEntity, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TicketEntity?)null);
            _tickets.Setup(r => r.AddAsync(It.IsAny<TicketEntity>(), It.IsAny<CancellationToken>()))
                .Callback<TicketEntity, CancellationToken>((t, _) => AddedTickets.Add(t)).Returns(Task.CompletedTask);

            Handler = new ConfirmPendingOrderHandler(_ticketTypes.Object, Operations.Object, _details.Object,
                PaymentRepo.Object, _tickets.Object, UnitOfWork.Object, _cache, _time);
        }

        public ConfirmPendingOrderHandler Handler { get; }
        public void AddPayment(PaymentEntity payment) => Payments.Add(payment);
        public void AddPayments(IEnumerable<PaymentEntity> payments) => Payments.AddRange(payments);
        public Task<ConfirmPendingOrderResponse> Handle(ConfirmPendingOrderCommand command) => Handler.Handle(command, default);
        public ConfirmPendingOrderCommand Command(decimal amount, string? bankReference = null) => new()
        { TransactionRef = PendingPayment.TransactionRef!, ActualAmount = amount, BankReferenceCode = bankReference };
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }
}

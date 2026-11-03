using System.Linq.Expressions;
using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetDailySales;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.Ticket.Queries;

public sealed class GetDailySalesHandlerTests
{
    [Fact]
    public async Task Handle_UsesOriginalPaymentCompletionAndGroupsTicketsOncePerOrder()
    {
        var date = new DateOnly(2026, 3, 1);
        var startOrderId = Guid.NewGuid();
        var splitOrderId = Guid.NewGuid();
        var nextDayOrderId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var orderRows = new List<Order>
        {
            new() { Id = startOrderId, Status = "COMPLETED", FinalAmount = 100m, IsDeleted = false },
            new() { Id = splitOrderId, Status = "completed", FinalAmount = 200m, IsDeleted = false },
            new() { Id = nextDayOrderId, Status = "COMPLETED", FinalAmount = 100m, IsDeleted = false }
        };
        var paymentRows = new List<Payment>
        {
            new() { OrderId = startOrderId, Status = "COMPLETED", Amount = 100m, PaymentMethod = "cash", CreatedAt = Utc(2026, 2, 28, 16, 50), PaymentTime = Utc(2026, 2, 28, 17, 0) },
            new() { OrderId = startOrderId, Status = "COMPLETED", Amount = 10m, PaymentMethod = "TRANSFER", CreatedAt = Utc(2026, 3, 1, 18, 0), PaymentTime = Utc(2026, 3, 1, 18, 0) },
            new() { OrderId = splitOrderId, Status = "COMPLETED", Amount = 200m, PaymentMethod = "TRANSFER", CreatedAt = Utc(2026, 2, 28, 16, 0), PaymentTime = Utc(2026, 2, 28, 18, 0) },
            new() { OrderId = splitOrderId, Status = "COMPLETED", Amount = 100m, PaymentMethod = "TRANSFER", PaymentTime = Utc(2026, 2, 28, 17, 30) },
            new() { OrderId = splitOrderId, Status = "COMPLETED", Amount = 100m, PaymentMethod = "transfer", PaymentTime = Utc(2026, 2, 28, 18, 0) },
            new() { OrderId = nextDayOrderId, Status = "COMPLETED", Amount = 100m, PaymentMethod = "CASH", PaymentTime = Utc(2026, 3, 1, 17, 0) }
        };
        var details = new List<OrderDetail>
        {
            new() { OrderId = startOrderId, ItemId = typeId, ItemType = "TICKET", Quantity = 1 },
            new() { OrderId = splitOrderId, ItemId = typeId, ItemType = "ticket", Quantity = 2 },
            new() { OrderId = nextDayOrderId, ItemId = typeId, ItemType = "TICKET", Quantity = 4 }
        };
        var entries = new List<EntryLog>
        {
            new() { TicketId = Guid.NewGuid(), Status = "allowed", ScanTime = Utc(2026, 2, 28, 17, 0) },
            new() { TicketId = Guid.NewGuid(), Status = "DENIED", ScanTime = Utc(2026, 2, 28, 17, 10) },
            new() { TicketId = Guid.NewGuid(), Status = "ALLOWED", ScanTime = Utc(2026, 3, 1, 17, 0) },
            new() { Status = "ALLOWED", ScanTime = Utc(2026, 2, 28, 17, 0) }
        };
        var handler = CreateHandler(orderRows, paymentRows, details,
            [new TicketTypeEntity { Id = typeId, Name = "Vé lượt người lớn" }], entries);

        var result = await handler.Handle(new GetDailySalesQuery { Date = date }, CancellationToken.None);

        Assert.Equal(date, result.Date);
        Assert.Equal(3, result.TotalTickets);
        Assert.Equal(2, result.TotalOrders);
        Assert.Equal(1.5m, result.AverageTicketsPerOrder);
        Assert.Equal(1, result.PeakHour);
        Assert.Equal(2, result.PeakHourTickets);
        Assert.Equal(24, result.HourlySales.Count);
        Assert.Equal(1, result.HourlySales[0].TicketCount);
        Assert.Equal(2, result.HourlySales[1].TicketCount);
        Assert.Equal(3, result.TicketTypes.Single().TicketCount);
        Assert.Equal(1, result.PaymentMethods.Single(method => method.Name == "Tiền mặt").TicketCount);
        Assert.Equal(2, result.PaymentMethods.Single(method => method.Name == "Chuyển khoản / QR").TicketCount);
        Assert.Equal(1, result.AdmissionCount);

        var nextDay = await handler.Handle(new GetDailySalesQuery { Date = date.AddDays(1) }, CancellationToken.None);
        Assert.Equal(4, nextDay.TotalTickets);
        Assert.Equal(1, nextDay.TotalOrders);
    }

    [Fact]
    public async Task Handle_ExcludesUnderpaidOrderAndReturnsEmptySalesWithNullPeak()
    {
        var orderId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var handler = CreateHandler(
            [new Order { Id = orderId, Status = "COMPLETED", FinalAmount = 100m, IsDeleted = false }],
            [new Payment { OrderId = orderId, Status = "COMPLETED", Amount = 50m, PaymentMethod = "CASH", PaymentTime = Utc(2026, 2, 28, 17, 0) }],
            [new OrderDetail { OrderId = orderId, ItemId = typeId, ItemType = "TICKET", Quantity = 2 }],
            [new TicketTypeEntity { Id = typeId, Name = "Vé lượt người lớn" }],
            []);

        var result = await handler.Handle(new GetDailySalesQuery { Date = new DateOnly(2026, 3, 1) }, CancellationToken.None);

        Assert.Equal(0, result.TotalTickets);
        Assert.Equal(0, result.TotalOrders);
        Assert.Equal(0m, result.AverageTicketsPerOrder);
        Assert.Null(result.PeakHour);
        Assert.Equal(0, result.PeakHourTickets);
        Assert.Empty(result.TicketTypes);
        Assert.Empty(result.PaymentMethods);
    }

    [Fact]
    public async Task Handle_ExcludesUnsuccessfulAndDeletedOrdersAndCountsMixedMethodsOnce()
    {
        var mixedId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        var cancelledId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var ids = new[] { mixedId, pendingId, deletedId, cancelledId };
        var payments = ids.Select(id => new Payment { OrderId = id, Status = "COMPLETED", Amount = 100m,
            PaymentMethod = "CASH", CreatedAt = Utc(2026, 2, 28, 17, 0), PaymentTime = Utc(2026, 2, 28, 18, 0) }).ToList();
        payments.Add(new Payment { OrderId = mixedId, Status = "COMPLETED", Amount = 50m,
            PaymentMethod = "TRANSFER", CreatedAt = Utc(2026, 2, 28, 17, 30), PaymentTime = Utc(2026, 2, 28, 17, 30) });
        var handler = CreateHandler(
            [new Order { Id = mixedId, Status = "COMPLETED", FinalAmount = 100m },
                new Order { Id = pendingId, Status = "PENDING", FinalAmount = 100m },
                new Order { Id = deletedId, Status = "COMPLETED", FinalAmount = 100m, IsDeleted = true },
                new Order { Id = cancelledId, Status = "CANCELLED", FinalAmount = 100m }], payments,
            ids.Select(id => new OrderDetail { OrderId = id, ItemId = typeId, ItemType = "TICKET", Quantity = 2 }).ToList(),
            [new TicketTypeEntity { Id = typeId, Name = "Vé lượt" }], []);

        var result = await handler.Handle(new GetDailySalesQuery { Date = new DateOnly(2026, 3, 1) }, CancellationToken.None);

        Assert.Equal(2, result.TotalTickets);
        Assert.Equal(1, result.TotalOrders);
        Assert.Equal("Thanh toán kết hợp", result.PaymentMethods.Single().Name);
        Assert.Equal(result.TotalTickets, result.PaymentMethods.Sum(item => item.TicketCount));
        Assert.Equal(result.TotalTickets, result.HourlySales.Sum(item => item.TicketCount));
        Assert.Equal(result.TotalTickets, result.TicketTypes.Sum(item => item.TicketCount));
    }

    private static GetDailySalesHandler CreateHandler(
        List<Order> orders,
        List<Payment> payments,
        List<OrderDetail> details,
        List<TicketTypeEntity> ticketTypes,
        List<EntryLog> entries)
    {
        var orderRepository = new Mock<IRepository<Order>>();
        var paymentRepository = new Mock<IRepository<Payment>>();
        var detailRepository = new Mock<IRepository<OrderDetail>>();
        var ticketTypeRepository = new Mock<IRepository<TicketTypeEntity>>();
        var entryRepository = new Mock<IRepository<EntryLog>>();
        SetupFind(orderRepository, orders);
        SetupFind(paymentRepository, payments);
        SetupFind(detailRepository, details);
        SetupFind(ticketTypeRepository, ticketTypes);
        SetupFind(entryRepository, entries);
        return new GetDailySalesHandler(
            orderRepository.Object,
            detailRepository.Object,
            paymentRepository.Object,
            ticketTypeRepository.Object,
            entryRepository.Object);
    }

    private static void SetupFind<T>(Mock<IRepository<T>> repository, List<T> rows) where T : class =>
        repository.Setup(item => item.FindAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<T, bool>> predicate, CancellationToken _) =>
                Task.FromResult<IEnumerable<T>>(rows.Where(predicate.Compile()).ToList()));

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}

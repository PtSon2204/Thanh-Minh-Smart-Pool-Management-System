using Microsoft.Extensions.Caching.Memory;
using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.CreatePendingOrder;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets;

public sealed class CreatePendingOrderHandlerTests
{
    [Theory]
    [InlineData("VE_LUOT")]
    [InlineData("VE_THANG")]
    public async Task Handle_OnlineTicket_CreatesPendingOrderAndKeepsEffectiveStartDate(string category)
    {
        var ticketTypeId = Guid.NewGuid();
        var startDate = new DateTime(2026, 10, 5, 16, 30, 0, DateTimeKind.Utc);
        var ticketType = new TicketTypeEntity
        {
            Id = ticketTypeId,
            Name = "QA ticket",
            TicketCategory = category,
            Price = 250,
            IsActive = true
        };
        Order? createdOrder = null;
        OrderDetail? createdDetail = null;
        Payment? createdPayment = null;

        var ticketTypeRepo = new Mock<IRepository<TicketTypeEntity>>();
        ticketTypeRepo.Setup(repo => repo.GetByIdAsync(ticketTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticketType);
        var orderRepo = new Mock<IRepository<Order>>();
        orderRepo.Setup(repo => repo.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => createdOrder = order)
            .Returns(Task.CompletedTask);
        var detailRepo = new Mock<IRepository<OrderDetail>>();
        detailRepo.Setup(repo => repo.AddAsync(It.IsAny<OrderDetail>(), It.IsAny<CancellationToken>()))
            .Callback<OrderDetail, CancellationToken>((detail, _) => createdDetail = detail)
            .Returns(Task.CompletedTask);
        var paymentRepo = new Mock<IRepository<Payment>>();
        paymentRepo.Setup(repo => repo.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => createdPayment = payment)
            .Returns(Task.CompletedTask);
        var userRepo = new Mock<IRepository<User>>();
        var voucherRepo = new Mock<IRepository<Voucher>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var handler = new CreatePendingOrderHandler(ticketTypeRepo.Object, orderRepo.Object, detailRepo.Object,
            paymentRepo.Object, userRepo.Object, voucherRepo.Object, unitOfWork.Object, cache);
        var response = await handler.Handle(new CreatePendingOrderCommand
        {
            Items = [new SellOfflineTicketItem { TicketTypeId = ticketTypeId, Quantity = 2 }],
            StartDate = startDate
        }, CancellationToken.None);

        Assert.NotNull(createdOrder);
        Assert.Equal("PENDING", createdOrder.Status);
        Assert.Equal(500, createdOrder.TotalAmount);
        Assert.Equal(500, createdOrder.FinalAmount);
        Assert.NotNull(createdDetail);
        Assert.Equal("TICKET", createdDetail.ItemType);
        Assert.Equal(ticketTypeId, createdDetail.ItemId);
        Assert.Equal(2, createdDetail.Quantity);
        Assert.Equal(250, createdDetail.UnitPrice);
        Assert.NotNull(createdPayment);
        Assert.Equal("BANK_TRANSFER", createdPayment.PaymentMethod);
        Assert.Equal("PENDING", createdPayment.Status);
        Assert.Equal(500, createdPayment.Amount);
        Assert.False(string.IsNullOrWhiteSpace(response.TransactionRef));
        Assert.True(cache.TryGetValue($"OrderStartDate_{createdOrder.Id}", out DateTime cachedStartDate));
        Assert.Equal(startDate, cachedStartDate);
    }
}

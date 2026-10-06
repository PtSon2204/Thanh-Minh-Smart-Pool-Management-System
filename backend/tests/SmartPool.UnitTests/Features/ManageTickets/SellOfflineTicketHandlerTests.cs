using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets;

public sealed class SellOfflineTicketHandlerTests
{
    [Theory]
    [InlineData("VE_LUOT", 2, 400)]
    [InlineData("VE_THANG", 3, 600)]
    public async Task Handle_CounterSale_PreservesCategoryTicketAndPaymentContract(
        string category, int quantity, decimal expectedTotal)
    {
        var typeId = Guid.NewGuid();
        var ticketType = new TicketTypeEntity
        {
            Id = typeId,
            Name = "QA ticket",
            TicketCategory = category,
            Price = 200,
            IsActive = true
        };
        var orderDetails = new List<OrderDetail>();
        var tickets = new List<Ticket>();
        Payment? payment = null;

        var ticketRepo = new Mock<IRepository<Ticket>>();
        ticketRepo.Setup(repo => repo.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((ticket, _) => tickets.Add(ticket))
            .Returns(Task.CompletedTask);
        var ticketTypeRepo = new Mock<IRepository<TicketTypeEntity>>();
        ticketTypeRepo.Setup(repo => repo.GetByIdAsync(typeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticketType);
        var orderRepo = new Mock<IRepository<Order>>();
        orderRepo.Setup(repo => repo.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var detailRepo = new Mock<IRepository<OrderDetail>>();
        detailRepo.Setup(repo => repo.AddAsync(It.IsAny<OrderDetail>(), It.IsAny<CancellationToken>()))
            .Callback<OrderDetail, CancellationToken>((detail, _) => orderDetails.Add(detail))
            .Returns(Task.CompletedTask);
        var userRepo = new Mock<IRepository<User>>();
        var paymentRepo = new Mock<IRepository<Payment>>();
        paymentRepo.Setup(repo => repo.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((createdPayment, _) => payment = createdPayment)
            .Returns(Task.CompletedTask);
        var voucherRepo = new Mock<IRepository<Voucher>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new SellOfflineTicketHandler(ticketRepo.Object, ticketTypeRepo.Object, orderRepo.Object,
            detailRepo.Object, userRepo.Object, paymentRepo.Object, voucherRepo.Object, unitOfWork.Object);

        var response = await handler.Handle(new SellOfflineTicketCommand
        {
            Items = [new SellOfflineTicketItem { TicketTypeId = typeId, Quantity = quantity }]
        }, CancellationToken.None);

        var detail = Assert.Single(orderDetails);
        Assert.Equal("TICKET", detail.ItemType);
        Assert.Equal(typeId, detail.ItemId);
        Assert.Equal(quantity, detail.Quantity);
        Assert.Equal(200, detail.UnitPrice);
        Assert.Equal(expectedTotal, response.TotalAmount);
        Assert.Equal(expectedTotal, response.FinalAmount);
        Assert.Equal(expectedTotal, payment!.Amount);
        Assert.Equal("CASH", payment.PaymentMethod);
        Assert.Equal("COMPLETED", payment.Status);

        if (category == "VE_LUOT")
        {
            Assert.Empty(tickets);
            Assert.Empty(response.Tickets);
        }
        else
        {
            Assert.Equal(quantity, tickets.Count);
            Assert.Equal(quantity, response.Tickets.Count);
            Assert.All(tickets, ticket =>
            {
                Assert.StartsWith("TKT-", ticket.QrCode);
                Assert.Equal(typeId, ticket.TicketTypeId);
                Assert.Equal("ACTIVE", ticket.Status);
            });
            Assert.All(response.Tickets, info =>
            {
                Assert.StartsWith("TKT-", info.QrCode);
                Assert.Equal("VE_THANG", info.TicketCategory);
            });
        }
    }
}

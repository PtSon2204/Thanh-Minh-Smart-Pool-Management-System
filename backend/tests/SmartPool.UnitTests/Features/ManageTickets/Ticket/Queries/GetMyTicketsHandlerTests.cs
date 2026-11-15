using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyTickets;
using SmartPool.Application.Interfaces.Repositories;
using TicketEntity = SmartPool.Domain.Entities.Ticket;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;
using UserEntity = SmartPool.Domain.Entities.User;

namespace SmartPool.UnitTests.Features.ManageTickets.MyTickets;

public sealed class GetMyTicketsHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Có vé của người dùng -> Lọc, sắp xếp mới nhất trước và map đúng DTO")]
    public async Task UTCID01_UserHasTickets_ReturnsFilteredOrderedMappedTickets()
    {
        var userId = Guid.NewGuid();
        var oldTicket = Ticket(userId, "QR-OLD", createdAt: new DateTime(2026, 1, 1));
        var newTicket = Ticket(userId, "QR-NEW", createdAt: new DateTime(2026, 2, 1));
        var userTickets = new List<TicketEntity> { oldTicket, newTicket };

        var ticketOrderOperations = new Mock<ITicketOrderOperations>();
        ticketOrderOperations.Setup(r => r.GetTicketsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userTickets);

        var typeRepo = new Mock<ITicketTypeOperations>();
        typeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketTypeEntity>
        {
            new TicketTypeEntity { Id = oldTicket.TicketTypeId, Name = "Vé lượt", TicketCategory = "VE_LUOT" },
            new TicketTypeEntity { Id = newTicket.TicketTypeId, Name = "Vé tháng", TicketCategory = "VE_THANG" },
        });
        var user = new UserEntity { Id = userId, Username = "customer_user" };
        var userRepo = new Mock<IUserOperations>();
        userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        using var cts = new CancellationTokenSource();
        var result = await Handler(ticketOrderOperations, typeRepo, userRepo)
            .Handle(new GetMyTicketsQuery { UserId = userId }, cts.Token);

        Assert.Equal(new[] { "QR-NEW", "QR-OLD" }, result.Select(x => x.QrCode));
        Assert.Equal("Vé tháng", result[0].TicketName);
        Assert.Equal("VE_THANG", result[0].TicketCategory);
        Assert.Equal("customer_user", result[0].CustomerName);
        Assert.Equal(newTicket.Id, result[0].Id);
        Assert.Equal(newTicket.IssueDate, result[0].IssueDate);
        Assert.Equal(newTicket.ExpiryDate, result[0].ExpiryDate);
        Assert.Equal("ACTIVE", result[0].Status);
        Assert.Equal("Vé lượt", result[1].TicketName);
        ticketOrderOperations.Verify(r => r.GetTicketsByUserIdAsync(userId, cts.Token), Times.Once);
        userRepo.Verify(r => r.GetByIdAsync(userId, cts.Token), Times.Once);
        typeRepo.Verify(r => r.GetAllAsync(cts.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Không có vé phù hợp -> Trả danh sách rỗng")]
    public async Task UTCID02_NoMatchingTickets_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();
        var (ticketRepo, typeRepo, userRepo) = Repositories(userId, new List<TicketEntity>(), new List<TicketTypeEntity>(), null);

        var result = await Handler(ticketRepo, typeRepo, userRepo)
            .Handle(new GetMyTicketsQuery { UserId = userId }, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact(DisplayName = "UTCID03 (A) - Loại vé không còn trong danh sách -> TicketName Unknown và category rỗng")]
    public async Task UTCID03_TicketTypeMissing_UsesFallbackValues()
    {
        var userId = Guid.NewGuid();
        var ticket = Ticket(userId, "QR-1");
        var (ticketRepo, typeRepo, userRepo) = Repositories(userId, [ticket], new List<TicketTypeEntity>(), new UserEntity { Id = userId, Username = "customer" });

        var result = await Handler(ticketRepo, typeRepo, userRepo)
            .Handle(new GetMyTicketsQuery { UserId = userId }, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("Unknown", dto.TicketName);
        Assert.Equal(string.Empty, dto.TicketCategory);
    }

    [Fact(DisplayName = "UTCID04 (A) - Không tìm thấy user -> CustomerName rỗng")]
    public async Task UTCID04_UserMissing_UsesEmptyCustomerName()
    {
        var userId = Guid.NewGuid();
        var ticket = Ticket(userId, "QR-1");
        var type = new TicketTypeEntity { Id = ticket.TicketTypeId, Name = "Vé lượt", TicketCategory = "VE_LUOT" };
        var (ticketRepo, typeRepo, userRepo) = Repositories(userId, [ticket], [type], null);

        var result = await Handler(ticketRepo, typeRepo, userRepo)
            .Handle(new GetMyTicketsQuery { UserId = userId }, CancellationToken.None);

        Assert.Equal(string.Empty, Assert.Single(result).CustomerName);
    }

    [Fact(DisplayName = "UTCID05 (B) - Trường nullable/status null -> Giữ ngày null và map status thành chuỗi rỗng")]
    public async Task UTCID05_NullOptionalTicketFields_AreMappedAsImplemented()
    {
        var userId = Guid.NewGuid();
        var ticket = Ticket(userId, "QR-NULL", status: null);
        ticket.IssueDate = null;
        ticket.ExpiryDate = null;
        var type = new TicketTypeEntity { Id = ticket.TicketTypeId, Name = "Vé lượt", TicketCategory = "VE_LUOT" };
        var (ticketRepo, typeRepo, userRepo) = Repositories(userId, [ticket], [type], new UserEntity { Id = userId, Username = "customer" });

        var result = await Handler(ticketRepo, typeRepo, userRepo)
            .Handle(new GetMyTicketsQuery { UserId = userId }, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Null(dto.IssueDate);
        Assert.Null(dto.ExpiryDate);
        Assert.Equal(string.Empty, dto.Status);
    }

    [Fact(DisplayName = "UTCID06 (A) - Repository ném exception -> Handler truyền tiếp exception")]
    public async Task UTCID06_TicketRepositoryFailure_IsPropagated()
    {
        var userId = Guid.NewGuid();
        var ticketRepo = new Mock<ITicketOrderOperations>();
        var failure = new InvalidOperationException("Ticket read failed");
        ticketRepo.Setup(r => r.GetTicketsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(ticketRepo, new Mock<ITicketTypeOperations>(), new Mock<IUserOperations>())
                .Handle(new GetMyTicketsQuery { UserId = userId }, CancellationToken.None));

        Assert.Same(failure, actual);
        ticketRepo.Verify(r => r.GetTicketsByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GetMyTicketsHandler Handler(
        Mock<ITicketOrderOperations> tickets,
        Mock<ITicketTypeOperations> ticketTypes,
        Mock<IUserOperations> users) => new(tickets.Object, ticketTypes.Object, users.Object);

    private static (Mock<ITicketOrderOperations>, Mock<ITicketTypeOperations>, Mock<IUserOperations>) Repositories(
        Guid userId, List<TicketEntity> tickets, List<TicketTypeEntity> ticketTypes, UserEntity? user)
    {
        var ticketRepo = new Mock<ITicketOrderOperations>();
        ticketRepo.Setup(r => r.GetTicketsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tickets);
        var typeRepo = new Mock<ITicketTypeOperations>();
        typeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ticketTypes);
        var userRepo = new Mock<IUserOperations>();
        userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return (ticketRepo, typeRepo, userRepo);
    }

    private static TicketEntity Ticket(Guid userId, string qrCode, DateTime? createdAt = null, bool? isDeleted = false,
        DateTime? issueDate = null, DateTime? expiryDate = null, string? status = "ACTIVE") => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TicketTypeId = Guid.NewGuid(),
        QrCode = qrCode,
        CreatedAt = createdAt ?? new DateTime(2026, 1, 1),
        IsDeleted = isDeleted,
        IssueDate = issueDate ?? new DateTime(2026, 1, 1),
        ExpiryDate = expiryDate ?? new DateTime(2026, 1, 2),
        Status = status,
    };
}

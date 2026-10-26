using System.Linq.Expressions;
using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using TicketEntity = SmartPool.Domain.Entities.Ticket;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.Ticket.Commands;

public class SellMonthlyTicketAtCounterTests
{
    [Fact(DisplayName = "UTCID01 (B) - Items null hoặc rỗng -> Báo giỏ hàng rỗng")]
    public async Task EmptyItems_ThrowsBeforeAnyRepositoryWrites()
    {
        var harness = new Harness();

        var emptyError = await Assert.ThrowsAsync<ArgumentException>(() => harness.Sell(new() { Items = [] }));
        var nullError = await Assert.ThrowsAsync<ArgumentException>(() => harness.Sell(new() { Items = null! }));

        Assert.Equal("Giỏ hàng rỗng.", emptyError.Message);
        Assert.Equal("Giỏ hàng rỗng.", nullError.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID02 (N) - Bán 1 vé tháng không có khách hàng -> Tạo vé QR hiệu lực 30 ngày và thanh toán tiền mặt")]
    public async Task SingleMonthlyTicket_WithoutCustomer_CreatesQrTicketValidForThirtyDays()
    {
        var harness = new Harness();

        var response = await harness.Sell(harness.Command(quantity: 1));

        var issued = Assert.Single(harness.CreatedTickets);
        Assert.Equal(500_000m, response.TotalAmount);
        Assert.Equal(0m, response.DiscountAmount);
        Assert.Equal(500_000m, response.FinalAmount);
        Assert.Null(response.AccountInfo);
        Assert.Equal(TicketCategoryEnum.VE_THANG.ToString(), Assert.Single(response.Tickets).TicketCategory);
        Assert.Equal(issued.Id, response.Tickets[0].Id);
        Assert.Matches("^\\d{6}$", issued.QrCode);
        Assert.Equal("ACTIVE", issued.Status);
        Assert.Equal(issued.IssueDate!.Value.AddDays(30), issued.ExpiryDate);
        harness.Orders.Verify(r => r.AddAsync(It.Is<Order>(o => o.TotalAmount == 500_000m
            && o.FinalAmount == 500_000m && o.Status == "COMPLETED" && o.UserId == null),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Payments.Verify(r => r.AddAsync(It.Is<Payment>(p => p.Amount == 500_000m
            && p.PaymentMethod == "CASH" && p.Status == "COMPLETED"), It.IsAny<CancellationToken>()), Times.Once);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (N) - Mua nhiều vé tháng -> Mỗi vé có QR riêng và hiệu lực 30 ngày")]
    public async Task MultipleMonthlyTickets_CreatesOneUniqueQrTicketPerQuantity()
    {
        var harness = new Harness();

        var response = await harness.Sell(harness.Command(quantity: 3));

        Assert.Equal(3, response.Tickets.Count);
        Assert.Equal(3, harness.CreatedTickets.Count);
        Assert.Equal(3, harness.CreatedTickets.Select(t => t.QrCode).Distinct().Count());
        Assert.All(harness.CreatedTickets, ticket =>
        {
            Assert.Matches("^\\d{6}$", ticket.QrCode);
            Assert.Equal(ticket.IssueDate!.Value.AddDays(30), ticket.ExpiryDate);
        });
        harness.OrderDetails.Verify(r => r.AddAsync(It.Is<OrderDetail>(d => d.Quantity == 3
            && d.UnitPrice == 500_000m && d.ItemType == "TICKET"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID04 (N) - Số điện thoại đã có tài khoản -> Gắn UserId vào đơn và vé tháng")]
    public async Task ExistingCustomer_IsLinkedToOrderAndIssuedTicket()
    {
        var customer = new User { Id = Guid.NewGuid(), Phone = "0900000001", Username = "0900000001" };
        var harness = new Harness(existingUser: customer);

        var response = await harness.Sell(harness.Command(phone: customer.Phone));

        Assert.Null(response.AccountInfo);
        Assert.Equal(customer.Id, Assert.Single(harness.CreatedTickets).UserId);
        harness.Orders.Verify(r => r.AddAsync(It.Is<Order>(o => o.UserId == customer.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 (N) - Số điện thoại chưa có tài khoản -> Tạo User và trả AccountInfo")]
    public async Task NewCustomer_CreatesAccountAndLinksMonthlyTicket()
    {
        var harness = new Harness();

        var response = await harness.Sell(harness.Command(phone: "0900000002"));

        Assert.NotNull(response.AccountInfo);
        Assert.Equal("0900000002", response.AccountInfo.Username);
        Assert.Equal(8, response.AccountInfo.Password.Length);
        Assert.Matches("[A-Z]", response.AccountInfo.Password);
        Assert.Matches("[a-z]", response.AccountInfo.Password);
        Assert.Matches("[2-9]", response.AccountInfo.Password);
        Assert.Matches("[@#!%*?&]", response.AccountInfo.Password);
        harness.Users.Verify(r => r.AddAsync(It.Is<User>(u => u.Phone == "0900000002"
            && u.Username == "0900000002" && u.Status == "ACTIVE"), It.IsAny<CancellationToken>()), Times.Once);
        harness.Orders.Verify(r => r.AddAsync(It.Is<Order>(o => o.UserId != null), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(harness.CreatedUsers.Single().Id, harness.CreatedTickets.Single().UserId);
    }

    [Fact(DisplayName = "UTCID06 (A) - TicketTypeId không tồn tại -> Báo lỗi và không tạo đơn")]
    public async Task MissingTicketType_ThrowsAndDoesNotCreateOrder()
    {
        var harness = new Harness(ticketTypeMissing: true);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Sell(harness.Command()));

        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID07 (A) - Loại vé tháng đang bị khóa -> Báo lỗi và không tạo đơn")]
    public async Task InactiveMonthlyTicketType_ThrowsAndDoesNotCreateOrder()
    {
        var harness = new Harness(ticketType: CreateMonthlyTicketType(isActive: false));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Sell(harness.Command()));

        Assert.Contains("đang bị khóa", error.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID08 (A) - Voucher không hợp lệ hoặc hết hạn -> Báo lỗi trước khi lưu")]
    public async Task InvalidVoucher_ThrowsBeforeCreatingOrder()
    {
        var harness = new Harness();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Sell(
            harness.Command(voucherCode: "SAI")));

        Assert.Contains("không hợp lệ hoặc đã hết hạn", error.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID09 (A) - Tổng đơn dưới MinOrderValue -> Báo lỗi và không lưu")]
    public async Task OrderBelowVoucherMinimum_ThrowsAndDoesNotCreateOrder()
    {
        var voucher = ValidVoucher("GIAM20K", "FIXED_AMOUNT", 20_000m);
        voucher.MinOrderValue = 600_000m;
        var harness = new Harness(voucher: voucher);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Sell(
            harness.Command(voucherCode: "GIAM20K")));

        Assert.Contains("Đơn hàng tối thiểu", error.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID10 (N) - Voucher phần trăm -> Tính đúng giảm giá và thanh toán tiền mặt")]
    public async Task PercentageVoucher_CalculatesDiscountAndFinalAmount()
    {
        var harness = new Harness(voucher: ValidVoucher("GIAM20", "PERCENTAGE", 20m));

        var response = await harness.Sell(harness.Command(voucherCode: "GIAM20"));

        Assert.Equal(500_000m, response.TotalAmount);
        Assert.Equal(100_000m, response.DiscountAmount);
        Assert.Equal(400_000m, response.FinalAmount);
        Assert.Single(response.Tickets);
        harness.Payments.Verify(r => r.AddAsync(It.Is<Payment>(p => p.Amount == 400_000m
            && p.PaymentMethod == "CASH"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID11 (B) - Voucher giảm cố định lớn hơn tổng tiền -> Giảm tối đa bằng tổng tiền")]
    public async Task FixedVoucher_IsCappedAtOrderTotal()
    {
        var harness = new Harness(voucher: ValidVoucher("FREE", "FIXED_AMOUNT", 600_000m));

        var response = await harness.Sell(harness.Command(voucherCode: "FREE"));

        Assert.Equal(500_000m, response.TotalAmount);
        Assert.Equal(500_000m, response.DiscountAmount);
        Assert.Equal(0m, response.FinalAmount);
        Assert.Single(response.Tickets);
        harness.Payments.Verify(r => r.AddAsync(It.Is<Payment>(p => p.Amount == 0m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID12 (B) - StartDate từ client ở tương lai -> Ngày phát hành vẫn lấy từ thời điểm mua và hạn 30 ngày")]
    public async Task ClientStartDate_IsIgnoredAndExpiryUsesIssueDate()
    {
        var harness = new Harness();
        var beforePurchase = DateTime.UtcNow;

        var response = await harness.Sell(harness.Command(startDate: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var ticket = Assert.Single(harness.CreatedTickets);
        Assert.InRange(ticket.IssueDate!.Value, beforePurchase, DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(ticket.IssueDate.Value.AddDays(30), ticket.ExpiryDate);
        Assert.Equal(ticket.IssueDate, response.Tickets[0].ExpiryDate!.Value.AddDays(-30));
    }

    [Fact(DisplayName = "UTCID13 (B) - Mã QR bị trùng lần đầu -> Sinh lại mã khác và hoàn tất bán vé")]
    public async Task DuplicateQrCode_RetriesAndCompletesSale()
    {
        var harness = new Harness(qrLookupBehavior: QrLookupBehavior.CollideOnce);

        var response = await harness.Sell(harness.Command());

        Assert.Single(response.Tickets);
        Assert.Single(harness.CreatedTickets);
        Assert.Equal(2, harness.QrLookupCount);
        Assert.Equal("ACTIVE", Assert.Single(harness.CreatedTickets).Status);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID14 (A) - Sinh QR trùng cả 10 lần -> Báo lỗi và không commit đơn")]
    public async Task DuplicateQrCodeTenTimes_ThrowsWithoutCommitting()
    {
        var harness = new Harness(qrLookupBehavior: QrLookupBehavior.AlwaysCollide);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Sell(harness.Command()));

        Assert.Contains("sau 10 lần thử", error.Message);
        Assert.Empty(harness.CreatedTickets);
        Assert.Equal(10, harness.QrLookupCount);
        harness.Payments.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TicketTypeEntity CreateMonthlyTicketType(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(), Name = "Vé tháng 30 ngày", TicketCategory = TicketCategoryEnum.VE_THANG.ToString(),
        Price = 500_000m, IsActive = isActive
    };

    private static Voucher ValidVoucher(string code, string discountType, decimal discountValue) => new()
    {
        Id = Guid.NewGuid(), Code = code, DiscountType = discountType, DiscountValue = discountValue,
        StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1), IsActive = true, IsDeleted = false
    };

    private enum QrLookupBehavior { Normal, CollideOnce, AlwaysCollide }

    private sealed class Harness
    {
        private readonly Guid ticketTypeId;
        private readonly Mock<IRepository<TicketEntity>> tickets = new();
        private readonly Mock<IRepository<TicketTypeEntity>> ticketTypes = new();
        private readonly Mock<IRepository<OrderDetail>> orderDetails = new();
        private readonly Mock<IRepository<User>> users = new();
        private readonly Mock<IRepository<Voucher>> vouchers = new();
        private readonly QrLookupBehavior qrLookupBehavior;

        public Mock<IRepository<Order>> Orders { get; } = new();
        public Mock<IRepository<Payment>> Payments { get; } = new();
        public Mock<IRepository<OrderDetail>> OrderDetails => orderDetails;
        public Mock<IRepository<User>> Users => users;
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public List<TicketEntity> CreatedTickets { get; } = [];
        public List<User> CreatedUsers { get; } = [];
        public int QrLookupCount { get; private set; }

        public Harness(TicketTypeEntity? ticketType = null, User? existingUser = null, Voucher? voucher = null,
            bool ticketTypeMissing = false, QrLookupBehavior qrLookupBehavior = QrLookupBehavior.Normal)
        {
            this.qrLookupBehavior = qrLookupBehavior;
            var configuredTicketType = ticketTypeMissing ? null : ticketType ?? CreateMonthlyTicketType();
            ticketTypeId = configuredTicketType?.Id ?? Guid.NewGuid();
            ticketTypes.Setup(r => r.GetByIdAsync(ticketTypeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(configuredTicketType);
            users.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<User, bool>> predicate, CancellationToken _) =>
                    Task.FromResult(existingUser != null && predicate.Compile()(existingUser) ? existingUser : null));
            users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback((User user, CancellationToken _) => CreatedUsers.Add(user))
                .Returns(Task.CompletedTask);
            vouchers.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Voucher, bool>> predicate, CancellationToken _) =>
                    Task.FromResult(voucher != null && predicate.Compile()(voucher) ? voucher : null));
            tickets.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TicketEntity, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<TicketEntity, bool>> _, CancellationToken _) =>
                {
                    QrLookupCount++;
                    return Task.FromResult<TicketEntity?>(qrLookupBehavior switch
                    {
                        QrLookupBehavior.AlwaysCollide => new TicketEntity { QrCode = "COLLISION" },
                        QrLookupBehavior.CollideOnce when QrLookupCount == 1 => new TicketEntity { QrCode = "COLLISION" },
                        _ => null
                    });
                });
            tickets.Setup(r => r.AddAsync(It.IsAny<TicketEntity>(), It.IsAny<CancellationToken>()))
                .Callback((TicketEntity ticket, CancellationToken _) => CreatedTickets.Add(ticket))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        public SellOfflineTicketCommand Command(int quantity = 1, string? phone = null, string? voucherCode = null,
            DateTime? startDate = null) => new()
        {
            Items = [new SellOfflineTicketItem { TicketTypeId = ticketTypeId, Quantity = quantity }],
            CustomerName = "Khách mua vé tháng", CustomerPhone = phone, VoucherCode = voucherCode, StartDate = startDate
        };

        public Task<SellOfflineTicketResponse> Sell(SellOfflineTicketCommand command) =>
            new SellOfflineTicketHandler(tickets.Object, ticketTypes.Object, Orders.Object, orderDetails.Object,
                users.Object, Payments.Object, vouchers.Object, UnitOfWork.Object, TimeProvider.System)
                .Handle(command, CancellationToken.None);
    }
}

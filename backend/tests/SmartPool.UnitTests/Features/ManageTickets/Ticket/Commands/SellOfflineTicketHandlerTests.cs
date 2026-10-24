using System.Linq.Expressions;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using Moq;
using TicketEntity = SmartPool.Domain.Entities.Ticket;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.Ticket.Commands;

public class SellOfflineTicketHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 5, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "UTCID01 (B) - Items null hoặc rỗng -> Báo giỏ hàng rỗng")]
    public async Task EmptyItems_ThrowsBeforeAnyRepositoryWrites()
    {
        var harness = new Harness();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => harness.Sell(new() { Items = [] }));
        var nullItemsError = await Assert.ThrowsAsync<ArgumentException>(() => harness.Sell(new() { Items = null! }));

        Assert.Equal("Giỏ hàng rỗng.", error.Message);
        Assert.Equal("Giỏ hàng rỗng.", nullItemsError.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID02 (N) - Bán 1 vé lượt, không có khách hàng -> Tạo đơn tiền mặt thành công")]
    public async Task SingleWalkInTicket_WithoutCustomer_CreatesCashOrderWithoutQrTicket()
    {
        var harness = new Harness();

        var response = await harness.Sell(harness.Command(quantity: 1));

        Assert.Equal(100_000m, response.TotalAmount);
        Assert.Equal(0m, response.DiscountAmount);
        Assert.Equal(100_000m, response.FinalAmount);
        Assert.Empty(response.Tickets);
        Assert.Null(response.AccountInfo);
        harness.Orders.Verify(r => r.AddAsync(It.Is<Order>(o => o.TotalAmount == 100_000m
            && o.FinalAmount == 100_000m && o.Status == "COMPLETED" && o.UserId == null),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Payments.Verify(r => r.AddAsync(It.Is<Payment>(p => p.Amount == 100_000m
            && p.PaymentMethod == "CASH" && p.Status == "COMPLETED"), It.IsAny<CancellationToken>()), Times.Once);
        harness.Tickets.Verify(r => r.AddAsync(It.IsAny<TicketEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (N) - Bán nhiều vé lượt -> Tính đúng số lượng và tổng tiền")]
    public async Task MultipleWalkInTickets_CalculatesQuantityAndTotals()
    {
        var harness = new Harness();

        var response = await harness.Sell(harness.Command(quantity: 3));

        Assert.Equal(300_000m, response.TotalAmount);
        Assert.Equal(300_000m, response.FinalAmount);
        harness.OrderDetails.Verify(r => r.AddAsync(It.Is<OrderDetail>(d => d.Quantity == 3
            && d.UnitPrice == 100_000m && d.ItemType == "TICKET"), It.IsAny<CancellationToken>()), Times.Once);
        harness.Tickets.Verify(r => r.AddAsync(It.IsAny<TicketEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID04 (N) - Số điện thoại đã có tài khoản -> Gắn đúng UserId vào đơn")]
    public async Task ExistingCustomer_IsLinkedToOrderWithoutCreatingAccount()
    {
        var existingUser = new User { Id = Guid.NewGuid(), Phone = "0900000001", Username = "0900000001" };
        var harness = new Harness(existingUser: existingUser);

        var response = await harness.Sell(harness.Command(phone: existingUser.Phone));

        Assert.Null(response.AccountInfo);
        harness.Orders.Verify(r => r.AddAsync(It.Is<Order>(o => o.UserId == existingUser.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 (N) - Số điện thoại chưa có tài khoản -> Tạo User và trả AccountInfo")]
    public async Task NewCustomer_CreatesAccountAndReturnsCredentials()
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
    }

    [Fact(DisplayName = "UTCID06 (A) - TicketTypeId không tồn tại -> Báo lỗi và không tạo đơn")]
    public async Task MissingTicketType_ThrowsAndDoesNotCreateOrder()
    {
        var harness = new Harness(ticketTypeMissing: true);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Sell(harness.Command()));

        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID07 (A) - Loại vé đang bị khóa -> Báo lỗi và không tạo đơn")]
    public async Task InactiveTicketType_ThrowsAndDoesNotCreateOrder()
    {
        var harness = new Harness(ticketType: CreateTicketType(isActive: false));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Sell(harness.Command()));

        Assert.Contains("đang bị khóa", error.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID08 (A) - Voucher không hợp lệ/hết hạn -> Báo lỗi trước khi lưu")]
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
        voucher.MinOrderValue = 150_000m;
        var harness = new Harness(voucher: voucher);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Sell(
            harness.Command(voucherCode: "GIAM20K")));

        Assert.Contains("Đơn hàng tối thiểu", error.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID10 (N) - Voucher phần trăm -> Tính đúng DiscountAmount và FinalAmount")]
    public async Task PercentageVoucher_CalculatesDiscountAndFinalAmount()
    {
        var harness = new Harness(voucher: ValidVoucher("GIAM20", "PERCENTAGE", 20m));

        var response = await harness.Sell(harness.Command(voucherCode: "GIAM20"));

        Assert.Equal(100_000m, response.TotalAmount);
        Assert.Equal(20_000m, response.DiscountAmount);
        Assert.Equal(80_000m, response.FinalAmount);
        Assert.Empty(response.Tickets);
        harness.Payments.Verify(r => r.AddAsync(It.Is<Payment>(p => p.Amount == 80_000m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID11 (B) - Voucher giảm cố định lớn hơn tổng tiền -> Giảm tối đa bằng tổng tiền")]
    public async Task FixedVoucher_IsCappedAtOrderTotal()
    {
        var harness = new Harness(voucher: ValidVoucher("FREE", "FIXED_AMOUNT", 150_000m));

        var response = await harness.Sell(harness.Command(voucherCode: "FREE"));

        Assert.Equal(100_000m, response.TotalAmount);
        Assert.Equal(100_000m, response.DiscountAmount);
        Assert.Equal(0m, response.FinalAmount);
        harness.Payments.Verify(r => r.AddAsync(It.Is<Payment>(p => p.Amount == 0m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID12 (N) - Voucher bắt đầu đúng thời điểm mua -> Áp dụng theo giờ server")]
    public async Task VoucherStartingAtCurrentTime_IsAccepted()
    {
        var voucher = ValidVoucher("GIAM20K", "FIXED_AMOUNT", 20_000m);
        voucher.StartDate = Now;
        var harness = new Harness(voucher: voucher);

        var response = await harness.Sell(harness.Command(voucherCode: " giam20k "));

        Assert.Equal(20_000m, response.DiscountAmount);
        Assert.Equal(80_000m, response.FinalAmount);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TicketTypeEntity CreateTicketType(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(), Name = "Vé lượt", TicketCategory = TicketCategoryEnum.VE_LUOT.ToString(),
        Price = 100_000m, IsActive = isActive
    };

    private static Voucher ValidVoucher(string code, string discountType, decimal discountValue) => new()
    {
        Id = Guid.NewGuid(), Code = code, DiscountType = discountType, DiscountValue = discountValue,
        StartDate = Now.AddDays(-1), EndDate = Now.AddDays(1), IsActive = true, IsDeleted = false
    };

    private sealed class Harness
    {
        private readonly Guid ticketTypeId;
        private readonly Mock<IRepository<TicketEntity>> tickets = new();
        private readonly Mock<IRepository<TicketTypeEntity>> ticketTypes = new();
        private readonly Mock<IRepository<OrderDetail>> orderDetails = new();
        private readonly Mock<IRepository<User>> users = new();
        private readonly Mock<IRepository<Voucher>> vouchers = new();

        public Mock<IRepository<Order>> Orders { get; } = new();
        public Mock<IRepository<Payment>> Payments { get; } = new();
        public Mock<IRepository<OrderDetail>> OrderDetails => orderDetails;
        public Mock<IRepository<User>> Users => users;
        public Mock<IRepository<TicketEntity>> Tickets => tickets;
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public Harness(TicketTypeEntity? ticketType = null, User? existingUser = null, Voucher? voucher = null,
            bool ticketTypeMissing = false)
        {
            var configuredTicketType = ticketTypeMissing ? null : ticketType ?? CreateTicketType();
            ticketTypeId = configuredTicketType?.Id ?? Guid.NewGuid();
            if (configuredTicketType is not null)
                ticketTypes.Setup(r => r.GetByIdAsync(ticketTypeId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(configuredTicketType);
            else
                ticketTypes.Setup(r => r.GetByIdAsync(ticketTypeId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((TicketTypeEntity?)null);

            users.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<User, bool>> predicate, CancellationToken _) =>
                    Task.FromResult(existingUser != null && predicate.Compile()(existingUser) ? existingUser : null));
            vouchers.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Voucher, bool>> predicate, CancellationToken _) =>
                    Task.FromResult(voucher != null && predicate.Compile()(voucher) ? voucher : null));
            tickets.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TicketEntity, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TicketEntity?)null);
            UnitOfWork.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        public SellOfflineTicketCommand Command(int quantity = 1, string? phone = null, string? voucherCode = null) => new()
        {
            Items = [new SellOfflineTicketItem { TicketTypeId = ticketTypeId, Quantity = quantity }],
            CustomerName = "Khách tại quầy", CustomerPhone = phone, VoucherCode = voucherCode
        };

        public Task<SellOfflineTicketResponse> Sell(SellOfflineTicketCommand command) =>
            new SellOfflineTicketHandler(tickets.Object, ticketTypes.Object, Orders.Object, orderDetails.Object,
                users.Object, Payments.Object, vouchers.Object, UnitOfWork.Object,
                new FixedTimeProvider(Now)).Handle(command, CancellationToken.None);
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}

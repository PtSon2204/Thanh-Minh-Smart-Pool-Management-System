using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
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
        harness.TicketOrderOperations.Verify(r => r.CreateOfflineSaleAsync(It.IsAny<Order>(), It.IsAny<IEnumerable<OrderDetail>>(), It.IsAny<Payment>(), It.IsAny<IEnumerable<TicketEntity>>(), It.IsAny<CancellationToken>()), Times.Never);
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
        Assert.Single(harness.CreatedOrders);
        Assert.Equal(100_000m, harness.CreatedOrders[0].TotalAmount);
        Assert.Equal(100_000m, harness.CreatedOrders[0].FinalAmount);
        Assert.Equal("COMPLETED", harness.CreatedOrders[0].Status);
        Assert.Null(harness.CreatedOrders[0].UserId);
        Assert.Single(harness.CreatedPayments);
        Assert.Equal(100_000m, harness.CreatedPayments[0].Amount);
        Assert.Equal("CASH", harness.CreatedPayments[0].PaymentMethod);
        Assert.Equal("COMPLETED", harness.CreatedPayments[0].Status);
        Assert.Empty(harness.CreatedTickets);
        harness.TicketOrderOperations.Verify(r => r.CreateOfflineSaleAsync(It.IsAny<Order>(), It.IsAny<IEnumerable<OrderDetail>>(), It.IsAny<Payment>(), It.IsAny<IEnumerable<TicketEntity>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (N) - Bán nhiều vé lượt -> Tính đúng số lượng và tổng tiền")]
    public async Task MultipleWalkInTickets_CalculatesQuantityAndTotals()
    {
        var harness = new Harness();

        var response = await harness.Sell(harness.Command(quantity: 3));

        Assert.Equal(300_000m, response.TotalAmount);
        Assert.Equal(300_000m, response.FinalAmount);
        var detail = Assert.Single(harness.CreatedDetails);
        Assert.Equal(3, detail.Quantity);
        Assert.Equal(100_000m, detail.UnitPrice);
        Assert.Equal("TICKET", detail.ItemType);
        Assert.Empty(harness.CreatedTickets);
    }

    [Fact(DisplayName = "UTCID04 (N) - Số điện thoại đã có tài khoản -> Gắn đúng UserId vào đơn")]
    public async Task ExistingCustomer_IsLinkedToOrderWithoutCreatingAccount()
    {
        var existingUser = new User { Id = Guid.NewGuid(), Phone = "0900000001", Username = "0900000001" };
        var harness = new Harness(existingUser: existingUser);

        var response = await harness.Sell(harness.Command(phone: existingUser.Phone));

        Assert.Null(response.AccountInfo);
        Assert.Equal(existingUser.Id, Assert.Single(harness.CreatedOrders).UserId);
        harness.UserOperations.Verify(r => r.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
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
        harness.UserOperations.Verify(r => r.AddUserAsync(It.Is<User>(u => u.Phone == "0900000002"
            && u.Username == "0900000002" && u.Status == "ACTIVE"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(Assert.Single(harness.CreatedOrders).UserId);
    }

    [Fact(DisplayName = "UTCID06 (A) - TicketTypeId không tồn tại -> Báo lỗi và không tạo đơn")]
    public async Task MissingTicketType_ThrowsAndDoesNotCreateOrder()
    {
        var harness = new Harness(ticketTypeMissing: true);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Sell(harness.Command()));

        harness.TicketOrderOperations.Verify(r => r.CreateOfflineSaleAsync(It.IsAny<Order>(), It.IsAny<IEnumerable<OrderDetail>>(), It.IsAny<Payment>(), It.IsAny<IEnumerable<TicketEntity>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID07 (A) - Loại vé đang bị khóa -> Báo lỗi và không tạo đơn")]
    public async Task InactiveTicketType_ThrowsAndDoesNotCreateOrder()
    {
        var harness = new Harness(ticketType: CreateTicketType(isActive: false));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Sell(harness.Command()));

        Assert.Contains("đang bị khóa", error.Message);
        harness.TicketOrderOperations.Verify(r => r.CreateOfflineSaleAsync(It.IsAny<Order>(), It.IsAny<IEnumerable<OrderDetail>>(), It.IsAny<Payment>(), It.IsAny<IEnumerable<TicketEntity>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID08 (A) - Voucher không hợp lệ/hết hạn -> Báo lỗi trước khi lưu")]
    public async Task InvalidVoucher_ThrowsBeforeCreatingOrder()
    {
        var harness = new Harness();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Sell(
            harness.Command(voucherCode: "SAI")));

        Assert.Contains("không hợp lệ hoặc đã hết hạn", error.Message);
        harness.TicketOrderOperations.Verify(r => r.CreateOfflineSaleAsync(It.IsAny<Order>(), It.IsAny<IEnumerable<OrderDetail>>(), It.IsAny<Payment>(), It.IsAny<IEnumerable<TicketEntity>>(), It.IsAny<CancellationToken>()), Times.Never);
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
        harness.TicketOrderOperations.Verify(r => r.CreateOfflineSaleAsync(It.IsAny<Order>(), It.IsAny<IEnumerable<OrderDetail>>(), It.IsAny<Payment>(), It.IsAny<IEnumerable<TicketEntity>>(), It.IsAny<CancellationToken>()), Times.Never);
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
        Assert.Equal(80_000m, Assert.Single(harness.CreatedPayments).Amount);
    }

    [Fact(DisplayName = "UTCID11 (B) - Voucher giảm cố định lớn hơn tổng tiền -> Giảm tối đa bằng tổng tiền")]
    public async Task FixedVoucher_IsCappedAtOrderTotal()
    {
        var harness = new Harness(voucher: ValidVoucher("FREE", "FIXED_AMOUNT", 150_000m));

        var response = await harness.Sell(harness.Command(voucherCode: "FREE"));

        Assert.Equal(100_000m, response.TotalAmount);
        Assert.Equal(100_000m, response.DiscountAmount);
        Assert.Equal(0m, response.FinalAmount);
        Assert.Equal(0m, Assert.Single(harness.CreatedPayments).Amount);
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
        harness.TicketOrderOperations.Verify(r => r.CreateOfflineSaleAsync(It.IsAny<Order>(), It.IsAny<IEnumerable<OrderDetail>>(), It.IsAny<Payment>(), It.IsAny<IEnumerable<TicketEntity>>(), It.IsAny<CancellationToken>()), Times.Once);
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
        public Mock<ITicketOrderOperations> TicketOrderOperations { get; } = new();
        public Mock<ITicketTypeOperations> TicketTypeOperations { get; } = new();
        public Mock<IUserOperations> UserOperations { get; } = new();
        public Mock<IVoucherOperations> VoucherOperations { get; } = new();

        public List<Order> CreatedOrders { get; } = [];
        public List<OrderDetail> CreatedDetails { get; } = [];
        public List<Payment> CreatedPayments { get; } = [];
        public List<TicketEntity> CreatedTickets { get; } = [];
        public List<User> CreatedUsers { get; } = [];

        public Harness(TicketTypeEntity? ticketType = null, User? existingUser = null, Voucher? voucher = null,
            bool ticketTypeMissing = false)
        {
            var configuredTicketType = ticketTypeMissing ? null : ticketType ?? CreateTicketType();
            ticketTypeId = configuredTicketType?.Id ?? Guid.NewGuid();
            if (configuredTicketType is not null)
                TicketTypeOperations.Setup(r => r.GetByIdAsync(ticketTypeId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(configuredTicketType);
            else
                TicketTypeOperations.Setup(r => r.GetByIdAsync(ticketTypeId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((TicketTypeEntity?)null);

            UserOperations.Setup(u => u.GetByPhoneOrUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string phone, CancellationToken _) => existingUser != null && existingUser.Phone == phone ? existingUser : null);
            UserOperations.Setup(u => u.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback((User user, CancellationToken _) => CreatedUsers.Add(user))
                .Returns(Task.CompletedTask);

            VoucherOperations.Setup(v => v.GetUsableVoucherByCodeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns((string code, DateTime now, CancellationToken _) =>
                    Task.FromResult(voucher != null && string.Equals(voucher.Code, code.Trim(), StringComparison.OrdinalIgnoreCase) && voucher.IsActive == true && voucher.IsDeleted != true && (voucher.StartDate == null || voucher.StartDate <= now) && (voucher.EndDate == null || voucher.EndDate >= now) ? voucher : null));

            TicketOrderOperations.Setup(r => r.TicketCodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            TicketOrderOperations.Setup(r => r.CreateOfflineSaleAsync(It.IsAny<Order>(), It.IsAny<IEnumerable<OrderDetail>>(), It.IsAny<Payment>(), It.IsAny<IEnumerable<TicketEntity>>(), It.IsAny<CancellationToken>()))
                .Callback((Order order, IEnumerable<OrderDetail> details, Payment payment, IEnumerable<TicketEntity> tickets, CancellationToken _) =>
                {
                    CreatedOrders.Add(order);
                    CreatedDetails.AddRange(details);
                    CreatedPayments.Add(payment);
                    CreatedTickets.AddRange(tickets);
                }).Returns(Task.CompletedTask);
        }

        public SellOfflineTicketCommand Command(int quantity = 1, string? phone = null, string? voucherCode = null) => new()
        {
            Items = [new SellOfflineTicketItem { TicketTypeId = ticketTypeId, Quantity = quantity }],
            CustomerName = "Khách tại quầy", CustomerPhone = phone, VoucherCode = voucherCode
        };

        public Task<SellOfflineTicketResponse> Sell(SellOfflineTicketCommand command) =>
            new SellOfflineTicketHandler(TicketOrderOperations.Object, TicketTypeOperations.Object,
                UserOperations.Object, VoucherOperations.Object,
                new FixedTimeProvider(Now)).Handle(command, CancellationToken.None);
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}

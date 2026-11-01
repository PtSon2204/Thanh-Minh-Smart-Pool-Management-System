using System.Linq.Expressions;
using MediatR;
using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.CheckoutOnline;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.Ticket.Commands;

public class CheckoutOnlineHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 5, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "UTCID01 (B) - Giỏ hàng null/rỗng hoặc Quantity không dương -> Báo lỗi trước khi truy vấn")]
    public async Task InvalidCart_ThrowsBeforeRepositoryReadsOrWrites()
    {
        var harness = new Harness();

        var emptyError = await Assert.ThrowsAsync<ArgumentException>(() => harness.Checkout(new() { Items = [] }));
        var nullError = await Assert.ThrowsAsync<ArgumentException>(() => harness.Checkout(new() { Items = null! }));
        var zeroError = await Assert.ThrowsAsync<ArgumentException>(() => harness.Checkout(harness.Command(quantity: 0)));
        var negativeError = await Assert.ThrowsAsync<ArgumentException>(() => harness.Checkout(harness.Command(quantity: -1)));

        Assert.All(new[] { emptyError, nullError, zeroError, negativeError }, error =>
            Assert.Equal("Giỏ hàng phải có vé với số lượng lớn hơn 0.", error.Message));
        harness.TicketTypes.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID02 (N) - Khách đặt 1 vé online không voucher -> Tạo đơn chờ chuyển khoản")]
    public async Task SingleTicket_WithoutVoucher_CreatesPendingTransferOrder()
    {
        var userId = Guid.NewGuid();
        var harness = new Harness();

        var response = await harness.Checkout(harness.Command(userId: userId));

        Assert.True(response.Success);
        Assert.Equal("PENDING", response.Status);
        Assert.Equal(500_000m, response.Subtotal);
        Assert.Equal(0m, response.DiscountAmount);
        Assert.Equal(500_000m, response.FinalAmount);
        Assert.Equal(500_000m, response.RemainingAmount);
        Assert.Equal(500_000m, response.TotalAmount);
        Assert.Empty(response.Tickets);
        Assert.Matches("^SP[0-9A-F]{6}$", response.TransactionRef);
        Assert.Equal(Now.Add(OnlineOrderPaymentPolicy.Lifetime), response.ExpiresAtUtc);
        Assert.Equal(Now, response.ServerTimeUtc);
        Assert.Equal(userId, Assert.Single(harness.CreatedOrders).UserId);
        Assert.Equal("PENDING", harness.CreatedOrders[0].Status);
        Assert.Equal("TRANSFER", Assert.Single(harness.CreatedPayments).PaymentMethod);
        Assert.Equal("PENDING", harness.CreatedPayments[0].Status);
        Assert.Equal(response.OrderId, harness.CreatedPayments[0].OrderId);
        harness.Sender.Verify(s => s.Send(It.IsAny<ConfirmPendingOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (N) - Giỏ hàng có nhiều loại vé -> Tính tổng và tạo chi tiết đúng số lượng")]
    public async Task MultipleTicketTypes_CalculatesSubtotalAndCreatesMatchingDetails()
    {
        var harness = new Harness(secondTicketType: CreateTicketType("Vé lượt online", 100_000m));
        var command = harness.Command(quantity: 2);
        command.Items.Add(new CheckoutItemDto { TicketTypeId = harness.SecondTicketTypeId, Quantity = 3 });

        var response = await harness.Checkout(command);

        Assert.Equal(1_300_000m, response.Subtotal);
        Assert.Equal(1_300_000m, response.FinalAmount);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(1_000_000m, response.Items[0].LineTotal);
        Assert.Equal(300_000m, response.Items[1].LineTotal);
        Assert.Equal(2, harness.CreatedDetails.Count);
        Assert.Contains(harness.CreatedDetails, d => d.ItemId == harness.FirstTicketTypeId && d.Quantity == 2 && d.UnitPrice == 500_000m);
        Assert.Contains(harness.CreatedDetails, d => d.ItemId == harness.SecondTicketTypeId && d.Quantity == 3 && d.UnitPrice == 100_000m);
    }

    [Fact(DisplayName = "UTCID04 (A) - TicketTypeId không tồn tại -> Báo lỗi và không tạo đơn")]
    public async Task MissingTicketType_ThrowsWithoutSavingOrder()
    {
        var harness = new Harness(ticketTypeMissing: true);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Checkout(harness.Command()));

        Assert.Empty(harness.CreatedOrders);
        Assert.Empty(harness.CreatedPayments);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 (A) - Loại vé bị khóa -> Báo lỗi và không tạo đơn")]
    public async Task InactiveTicketType_ThrowsWithoutSavingOrder()
    {
        var harness = new Harness(firstTicketType: CreateTicketType("Vé tháng đã khóa", 500_000m, active: false));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Checkout(harness.Command()));

        Assert.Contains("đang bị khóa", error.Message);
        Assert.Empty(harness.CreatedOrders);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID06 (A) - Voucher không hợp lệ hoặc hết hạn -> Báo lỗi và không lưu")]
    public async Task InvalidVoucher_ThrowsWithoutSavingOrder()
    {
        var harness = new Harness();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Checkout(
            harness.Command(voucherCode: "SAI")));

        Assert.Contains("không hợp lệ hoặc đã hết hạn", error.Message);
        Assert.Empty(harness.CreatedOrders);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID07 (A) - Tổng đơn thấp hơn MinOrderValue -> Báo lỗi và không lưu")]
    public async Task BelowVoucherMinimum_ThrowsWithoutSavingOrder()
    {
        var voucher = ValidVoucher("GIAM20", "PERCENTAGE", 20m);
        voucher.MinOrderValue = 600_000m;
        var harness = new Harness(voucher: voucher);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Checkout(
            harness.Command(voucherCode: "GIAM20")));

        Assert.Contains("Đơn hàng tối thiểu", error.Message);
        Assert.Empty(harness.CreatedOrders);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID08 (N) - Voucher phần trăm hợp lệ -> Cập nhật đúng số tiền giảm và còn phải thanh toán")]
    public async Task PercentageVoucher_CalculatesDiscountAndRemainingAmount()
    {
        var harness = new Harness(voucher: ValidVoucher("GIAM20", "PERCENTAGE", 20m));

        var response = await harness.Checkout(harness.Command(voucherCode: " giam20 "));

        Assert.Equal(500_000m, response.Subtotal);
        Assert.Equal(100_000m, response.DiscountAmount);
        Assert.Equal(400_000m, response.FinalAmount);
        Assert.Equal(400_000m, response.RemainingAmount);
        Assert.Equal(400_000m, response.TotalAmount);
        Assert.Equal("PENDING", response.Status);
        Assert.Equal(harness.VoucherId, Assert.Single(harness.CreatedOrders).VoucherId);
        Assert.Equal(400_000m, Assert.Single(harness.CreatedPayments).Amount);
        harness.Sender.Verify(s => s.Send(It.IsAny<ConfirmPendingOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID09 (N) - Voucher giảm tiền hợp lệ dưới tổng đơn -> Giữ trạng thái chờ thanh toán")]
    public async Task FixedVoucherBelowTotal_CreatesPendingOrderForBalance()
    {
        var harness = new Harness(voucher: ValidVoucher("GIAM50K", "FIXED_AMOUNT", 50_000m));

        var response = await harness.Checkout(harness.Command(voucherCode: "GIAM50K"));

        Assert.Equal(500_000m, response.Subtotal);
        Assert.Equal(50_000m, response.DiscountAmount);
        Assert.Equal(450_000m, response.FinalAmount);
        Assert.Equal(450_000m, response.RemainingAmount);
        Assert.Equal("PENDING", response.Status);
        Assert.Equal(450_000m, Assert.Single(harness.CreatedPayments).Amount);
    }

    [Fact(DisplayName = "UTCID10 (B) - Voucher giảm bằng hoặc vượt tổng đơn -> Gọi xác nhận thanh toán 0đ và trả đơn hoàn tất")]
    public async Task FullyDiscountedOrder_ConfirmsZeroAmountAndReturnsCompletedResponse()
    {
        var ticket = new TicketInfo { Id = Guid.NewGuid(), QrCode = "123456", TicketCategory = "VE_THANG" };
        var confirmation = new ConfirmPendingOrderResponse { Success = true, Tickets = [ticket] };
        var harness = new Harness(voucher: ValidVoucher("FREE", "FIXED_AMOUNT", 600_000m), confirmation: confirmation);

        var response = await harness.Checkout(harness.Command(voucherCode: "FREE"));

        Assert.Equal(500_000m, response.Subtotal);
        Assert.Equal(500_000m, response.DiscountAmount);
        Assert.Equal(0m, response.FinalAmount);
        Assert.Equal(0m, response.RemainingAmount);
        Assert.Equal("COMPLETED", response.Status);
        Assert.Equal(ticket.Id, Assert.Single(response.Tickets).Id);
        Assert.Equal(0m, Assert.Single(harness.CreatedPayments).Amount);
        harness.Sender.Verify(s => s.Send(It.Is<ConfirmPendingOrderCommand>(c =>
            c.ActualAmount == 0 && c.TransactionRef == response.TransactionRef), It.IsAny<CancellationToken>()), Times.Once);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID11 (A) - Xác nhận đơn 0đ thất bại -> Ném lỗi sau khi lưu đơn chờ")]
    public async Task FailedZeroAmountConfirmation_ThrowsAfterInitialSave()
    {
        var harness = new Harness(voucher: ValidVoucher("FREE", "FIXED_AMOUNT", 500_000m),
            confirmation: new ConfirmPendingOrderResponse { Success = false, Message = "Confirmation rejected." });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Checkout(
            harness.Command(voucherCode: "FREE")));

        Assert.Equal("Confirmation rejected.", error.Message);
        Assert.Single(harness.CreatedOrders);
        Assert.Single(harness.CreatedPayments);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.Sender.Verify(s => s.Send(It.IsAny<ConfirmPendingOrderCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID09 (N) - Vé lượt không yêu cầu họ tên trong hồ sơ -> Cho phép thanh toán thành công")]
    public async Task SingleTicket_WithoutProfile_Succeeds()
    {
        var singleTicketType = new TicketTypeEntity
        {
            Id = Guid.NewGuid(),
            Name = "Vé lượt người lớn",
            TicketCategory = TicketCategoryEnum.VE_LUOT.ToString(),
            Price = 50_000m,
            IsActive = true
        };
        var harness = new Harness(firstTicketType: singleTicketType, userProfileMissing: true);

        var response = await harness.Checkout(harness.Command());

        Assert.True(response.Success);
        Assert.Equal("PENDING", response.Status);
        Assert.Equal(50_000m, response.FinalAmount);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID10 (B) - Vé tháng nhưng hồ sơ không tồn tại -> Chặn tạo đơn")]
    public async Task MonthlyTicket_WithoutProfile_ThrowsInvalidOperationException()
    {
        var harness = new Harness(userProfileMissing: true);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Checkout(harness.Command()));

        Assert.Equal("Vui lòng cập nhật họ và tên trong hồ sơ cá nhân để mua vé tháng.", error.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Payments.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID11 (B) - Vé tháng với họ tên chỉ có khoảng trắng -> Chặn tạo đơn")]
    public async Task MonthlyTicket_WithWhitespaceFullName_ThrowsInvalidOperationException()
    {
        var profile = new UserProfile { UserId = Guid.NewGuid(), FullName = "   " };
        var harness = new Harness(userProfile: profile);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Checkout(harness.Command()));

        Assert.Equal("Vui lòng cập nhật họ và tên trong hồ sơ cá nhân để mua vé tháng.", error.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Payments.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID12 (N) - Vé tháng với họ tên hợp lệ -> Cho phép tạo đơn")]
    public async Task MonthlyTicket_WithValidFullName_Succeeds()
    {
        var profile = new UserProfile { UserId = Guid.NewGuid(), FullName = "Trần Thị B" };
        var harness = new Harness(userProfile: profile);

        var response = await harness.Checkout(harness.Command());

        Assert.True(response.Success);
        Assert.Equal("PENDING", response.Status);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID13 (B) - Giỏ hỗn hợp vé lượt + vé tháng khi thiếu họ tên -> Chặn tạo đơn, không ghi database")]
    public async Task MixedTickets_WithoutFullName_ThrowsBeforeCreatingOrder()
    {
        var monthlyType = CreateTicketType("Vé tháng", 300_000m);
        var singleType = new TicketTypeEntity
        {
            Id = Guid.NewGuid(),
            Name = "Vé lượt",
            TicketCategory = TicketCategoryEnum.VE_LUOT.ToString(),
            Price = 50_000m,
            IsActive = true
        };
        var harness = new Harness(firstTicketType: monthlyType, secondTicketType: singleType, userProfileMissing: true);
        var command = new CheckoutOnlineCommand
        {
            UserId = Guid.NewGuid(),
            Items =
            [
                new CheckoutItemDto { TicketTypeId = monthlyType.Id, Quantity = 1 },
                new CheckoutItemDto { TicketTypeId = singleType.Id, Quantity = 2 }
            ]
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Checkout(command));

        Assert.Equal("Vui lòng cập nhật họ và tên trong hồ sơ cá nhân để mua vé tháng.", error.Message);
        harness.Orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.OrderDetails.Verify(r => r.AddAsync(It.IsAny<OrderDetail>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Payments.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.UnitOfWork.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID14 (N) - Giỏ hỗn hợp vé lượt + vé tháng có họ tên hợp lệ -> Tạo đúng 1 đơn chung")]
    public async Task MixedTickets_WithValidFullName_CreatesSingleOrder()
    {
        var monthlyType = CreateTicketType("Vé tháng", 300_000m);
        var singleType = new TicketTypeEntity
        {
            Id = Guid.NewGuid(),
            Name = "Vé lượt",
            TicketCategory = TicketCategoryEnum.VE_LUOT.ToString(),
            Price = 50_000m,
            IsActive = true
        };
        var profile = new UserProfile { UserId = Guid.NewGuid(), FullName = "Lê Văn C" };
        var harness = new Harness(firstTicketType: monthlyType, secondTicketType: singleType, userProfile: profile);
        var command = new CheckoutOnlineCommand
        {
            UserId = Guid.NewGuid(),
            Items =
            [
                new CheckoutItemDto { TicketTypeId = monthlyType.Id, Quantity = 1 },
                new CheckoutItemDto { TicketTypeId = singleType.Id, Quantity = 2 }
            ]
        };

        var response = await harness.Checkout(command);

        Assert.True(response.Success);
        Assert.Equal(400_000m, response.FinalAmount);
        Assert.Single(harness.CreatedOrders);
        Assert.Equal(2, harness.CreatedDetails.Count);
        Assert.Single(harness.CreatedPayments);
    }

    private static TicketTypeEntity CreateTicketType(string name, decimal price, bool active = true) => new()
    {
        Id = Guid.NewGuid(), Name = name, TicketCategory = TicketCategoryEnum.VE_THANG.ToString(),
        Price = price, IsActive = active
    };

    private static Voucher ValidVoucher(string code, string discountType, decimal discountValue) => new()
    {
        Id = Guid.NewGuid(), Code = code, DiscountType = discountType, DiscountValue = discountValue,
        StartDate = Now.AddDays(-1), EndDate = Now.AddDays(1), IsActive = true, IsDeleted = false
    };

    private sealed class Harness
    {
        private readonly Dictionary<Guid, TicketTypeEntity> ticketTypesById = [];
        private readonly Mock<IRepository<TicketTypeEntity>> ticketTypes = new();
        private readonly Mock<IRepository<Voucher>> vouchers = new();
        private readonly Mock<IRepository<OrderDetail>> orderDetails = new();
        private readonly Mock<IRepository<Payment>> payments = new();
        private readonly Mock<ISender> sender = new();
        private readonly Mock<IRepository<UserProfile>> userProfiles = new();

        public Mock<IRepository<Order>> Orders { get; } = new();
        public Mock<IRepository<OrderDetail>> OrderDetails => orderDetails;
        public Mock<IRepository<Payment>> Payments => payments;
        public Mock<IRepository<UserProfile>> UserProfiles => userProfiles;
        public Mock<IRepository<TicketTypeEntity>> TicketTypes => ticketTypes;
        public Mock<ISender> Sender => sender;
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public List<Order> CreatedOrders { get; } = [];
        public List<OrderDetail> CreatedDetails { get; } = [];
        public List<Payment> CreatedPayments { get; } = [];
        public Guid FirstTicketTypeId { get; }
        public Guid SecondTicketTypeId { get; }
        public Guid? VoucherId { get; }

        public Harness(TicketTypeEntity? firstTicketType = null, TicketTypeEntity? secondTicketType = null,
            Voucher? voucher = null, ConfirmPendingOrderResponse? confirmation = null, bool ticketTypeMissing = false)
        {
            var first = ticketTypeMissing ? null : firstTicketType ?? CreateTicketType("Vé tháng online", 500_000m);
            FirstTicketTypeId = first?.Id ?? Guid.NewGuid();
            if (first != null) ticketTypesById[first.Id] = first;
            if (secondTicketType != null) ticketTypesById[secondTicketType.Id] = secondTicketType;
            SecondTicketTypeId = secondTicketType?.Id ?? Guid.Empty;
            VoucherId = voucher?.Id;

            ticketTypes.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, CancellationToken _) => Task.FromResult(ticketTypesById.GetValueOrDefault(id)));
            vouchers.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Voucher, bool>> predicate, CancellationToken _) =>
                    Task.FromResult(voucher != null && predicate.Compile()(voucher) ? voucher : null));
            Orders.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .Callback((Order order, CancellationToken _) => CreatedOrders.Add(order)).Returns(Task.CompletedTask);
            orderDetails.Setup(r => r.AddAsync(It.IsAny<OrderDetail>(), It.IsAny<CancellationToken>()))
                .Callback((OrderDetail detail, CancellationToken _) => CreatedDetails.Add(detail)).Returns(Task.CompletedTask);
            payments.Setup(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
                .Callback((Payment payment, CancellationToken _) => CreatedPayments.Add(payment)).Returns(Task.CompletedTask);
            UnitOfWork.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            if (confirmation != null)
                sender.Setup(s => s.Send(It.IsAny<ConfirmPendingOrderCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(confirmation);
        }

        public CheckoutOnlineCommand Command(int quantity = 1, Guid? userId = null, string? voucherCode = null) => new()
        {
            UserId = userId ?? Guid.NewGuid(), VoucherCode = voucherCode,
            Items = [new CheckoutItemDto { TicketTypeId = FirstTicketTypeId, Quantity = quantity }]
        };

        public Task<CheckoutOnlineResponse> Checkout(CheckoutOnlineCommand command) =>
            new CheckoutOnlineHandler(ticketTypes.Object, Orders.Object, orderDetails.Object, payments.Object,
                vouchers.Object, UnitOfWork.Object, new FixedTimeProvider(Now), sender.Object)
                .Handle(command, CancellationToken.None);
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}

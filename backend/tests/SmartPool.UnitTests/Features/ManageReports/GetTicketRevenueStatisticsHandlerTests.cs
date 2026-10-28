using Moq;
using SmartPool.Application.Features.ManageReports.Queries.GetTicketRevenueStatistics;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageReports;

public sealed class GetTicketRevenueStatisticsHandlerTests
{
    private readonly Mock<IRepository<Order>> _orderRepo = new();
    private readonly Mock<IRepository<OrderDetail>> _orderDetailRepo = new();
    private readonly Mock<IRepository<Payment>> _paymentRepo = new();
    private readonly Mock<IRepository<Ticket>> _ticketRepo = new();
    private readonly Mock<IRepository<TicketType>> _ticketTypeRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IRepository<UserProfile>> _profileRepo = new();

    private GetTicketRevenueStatisticsHandler CreateHandler()
    {
        return new GetTicketRevenueStatisticsHandler(
            _orderRepo.Object,
            _orderDetailRepo.Object,
            _paymentRepo.Object,
            _ticketRepo.Object,
            _ticketTypeRepo.Object,
            _userRepo.Object,
            _profileRepo.Object);
    }

    private void SetupEmptyRepositories()
    {
        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order>());
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<OrderDetail>());
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType>());
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());
    }

    [Fact(DisplayName = "UTCID01 (N) - Không có dữ liệu trong DB -> Trả về dữ liệu demo hợp lệ 4 loại vé")]
    public async Task UTCID01_WhenNoOrdersInDatabase_ReturnsDemoData()
    {
        SetupEmptyRepositories();
        var handler = CreateHandler();
        var query = new GetTicketRevenueStatisticsQuery { Period = "7days" };

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.TotalTicketsSold > 0);
        Assert.True(result.TotalRevenue > 0);
        Assert.Equal(4, result.TicketTypeSales.Count);
        Assert.Contains(result.TicketTypeSales, t => t.Name == "Vé lượt người lớn");
        Assert.Contains(result.TicketTypeSales, t => t.Name == "Vé lượt trẻ em");
        Assert.Contains(result.TicketTypeSales, t => t.Name == "Vé tháng người lớn");
        Assert.Contains(result.TicketTypeSales, t => t.Name == "Vé tháng trẻ em");
    }

    [Fact(DisplayName = "UTCID02 (N) - Có đơn hàng vé lượt hợp lệ -> Tính đúng tổng số vé và tổng doanh thu")]
    public async Task UTCID02_SingleValidOrder_CalculatesRevenueAndTicketCount()
    {
        var now = DateTime.UtcNow;
        var ticketTypeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var tt = new TicketType { Id = ticketTypeId, Name = "Vé lượt người lớn", Price = 50_000, TicketCategory = "VE_LUOT" };
        var order = new Order { Id = orderId, TotalAmount = 100_000, FinalAmount = 100_000, IsDeleted = false, CreatedAt = now };
        var detail = new OrderDetail { Id = Guid.NewGuid(), OrderId = orderId, ItemId = ticketTypeId, ItemType = "TICKET", Quantity = 2, UnitPrice = 50_000 };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { order });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<OrderDetail> { detail });
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Equal(2, result.TotalTicketsSold);
        Assert.Equal(100_000m, result.TotalRevenue);
        Assert.Equal(100_000m, result.AverageOrderValue);
    }

    [Fact(DisplayName = "UTCID03 (N) - Có cả vé lượt và vé tháng -> Phân nhóm doanh thu theo từng loại vé chính xác")]
    public async Task UTCID03_MixedTicketTypes_GroupsRevenueCorrectly()
    {
        var now = DateTime.UtcNow;
        var ttSingle = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000 };
        var ttMonthly = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", Price = 500_000 };

        var o1 = new Order { Id = Guid.NewGuid(), FinalAmount = 50_000, IsDeleted = false, CreatedAt = now };
        var o2 = new Order { Id = Guid.NewGuid(), FinalAmount = 500_000, IsDeleted = false, CreatedAt = now };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = o1.Id, ItemId = ttSingle.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 50_000 },
            new() { Id = Guid.NewGuid(), OrderId = o2.Id, ItemId = ttMonthly.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 500_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { o1, o2 });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { ttSingle, ttMonthly });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Equal(2, result.TotalTicketsSold);
        Assert.Equal(550_000m, result.TotalRevenue);
        Assert.Equal(2, result.TicketTypeRevenues.Count);
        Assert.Contains(result.TicketTypeRevenues, r => r.Name == "Vé lượt người lớn" && r.Revenue == 50_000);
        Assert.Contains(result.TicketTypeRevenues, r => r.Name == "Vé tháng người lớn" && r.Revenue == 500_000);
    }

    [Fact(DisplayName = "UTCID04 (N) - Nhiều đơn cùng mua 1 loại vé -> Cộng dồn số lượng và doanh thu đúng")]
    public async Task UTCID04_MultipleOrdersSameType_AccumulatesRevenueAndQuantity()
    {
        var now = DateTime.UtcNow;
        var ttChild = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt trẻ em", Price = 30_000 };

        var o1 = new Order { Id = Guid.NewGuid(), FinalAmount = 30_000, IsDeleted = false, CreatedAt = now };
        var o2 = new Order { Id = Guid.NewGuid(), FinalAmount = 60_000, IsDeleted = false, CreatedAt = now };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = o1.Id, ItemId = ttChild.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 30_000 },
            new() { Id = Guid.NewGuid(), OrderId = o2.Id, ItemId = ttChild.Id, ItemType = "TICKET", Quantity = 2, UnitPrice = 30_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { o1, o2 });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { ttChild });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Equal(3, result.TotalTicketsSold);
        Assert.Equal(90_000m, result.TotalRevenue);
        var typeRev = result.TicketTypeRevenues.First(r => r.Name == "Vé lượt trẻ em");
        Assert.Equal(90_000m, typeRev.Revenue);
        Assert.Equal(3, typeRev.Count);
    }

    [Fact(DisplayName = "UTCID05 (N) - Bộ lọc period = today -> Chỉ tính các đơn hàng phát sinh trong ngày hôm nay")]
    public async Task UTCID05_PeriodToday_OnlyCalculatesTodayOrders()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000 };

        var todayOrder = new Order { Id = Guid.NewGuid(), FinalAmount = 50_000, IsDeleted = false, CreatedAt = now };
        var yesterdayOrder = new Order { Id = Guid.NewGuid(), FinalAmount = 50_000, IsDeleted = false, CreatedAt = now.AddDays(-1) };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = todayOrder.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 50_000 },
            new() { Id = Guid.NewGuid(), OrderId = yesterdayOrder.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 50_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { todayOrder, yesterdayOrder });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Equal(1, result.TotalTicketsSold);
        Assert.Equal(50_000m, result.TotalRevenue);
    }

    [Fact(DisplayName = "UTCID06 (N) - Bộ lọc period = 7days -> Tính các đơn hàng trong vòng 7 ngày qua")]
    public async Task UTCID06_Period7Days_IncludesOrdersWithinSevenDays()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000 };

        var orderRecent = new Order { Id = Guid.NewGuid(), FinalAmount = 50_000, IsDeleted = false, CreatedAt = now.AddDays(-3) };
        var orderOld = new Order { Id = Guid.NewGuid(), FinalAmount = 100_000, IsDeleted = false, CreatedAt = now.AddDays(-15) };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = orderRecent.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 50_000 },
            new() { Id = Guid.NewGuid(), OrderId = orderOld.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 2, UnitPrice = 50_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { orderRecent, orderOld });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "7days" }, CancellationToken.None);

        Assert.Equal(1, result.TotalTicketsSold);
        Assert.Equal(50_000m, result.TotalRevenue);
    }

    [Fact(DisplayName = "UTCID07 (N) - Bộ lọc period = 30days -> Tính các đơn hàng trong vòng 30 ngày qua")]
    public async Task UTCID07_Period30Days_IncludesOrdersWithinThirtyDays()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng trẻ em", Price = 300_000 };

        var order20DaysAgo = new Order { Id = Guid.NewGuid(), FinalAmount = 300_000, IsDeleted = false, CreatedAt = now.AddDays(-20) };
        var order40DaysAgo = new Order { Id = Guid.NewGuid(), FinalAmount = 300_000, IsDeleted = false, CreatedAt = now.AddDays(-40) };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = order20DaysAgo.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 300_000 },
            new() { Id = Guid.NewGuid(), OrderId = order40DaysAgo.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 300_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { order20DaysAgo, order40DaysAgo });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "30days" }, CancellationToken.None);

        Assert.Equal(1, result.TotalTicketsSold);
        Assert.Equal(300_000m, result.TotalRevenue);
    }

    [Fact(DisplayName = "UTCID08 (N) - Bộ lọc tùy chỉnh custom -> Tính chính xác đơn trong khoảng fromDate đến toDate")]
    public async Task UTCID08_PeriodCustom_FiltersCorrectlyBetweenDateRange()
    {
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000 };

        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);

        var matchOrder = new Order { Id = Guid.NewGuid(), FinalAmount = 50_000, IsDeleted = false, CreatedAt = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc) };
        var outsideOrder = new Order { Id = Guid.NewGuid(), FinalAmount = 50_000, IsDeleted = false, CreatedAt = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc) };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = matchOrder.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 50_000 },
            new() { Id = Guid.NewGuid(), OrderId = outsideOrder.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 50_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { matchOrder, outsideOrder });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "custom", FromDate = from, ToDate = to }, CancellationToken.None);

        Assert.Equal(1, result.TotalTicketsSold);
        Assert.Equal(50_000m, result.TotalRevenue);
    }

    [Fact(DisplayName = "UTCID09 (A) - Đơn hàng bị xóa mềm (IsDeleted = true) -> Loại bỏ không tính vào doanh thu")]
    public async Task UTCID09_SoftDeletedOrder_IsExcluded()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000 };

        var validOrder = new Order { Id = Guid.NewGuid(), FinalAmount = 50_000, IsDeleted = false, CreatedAt = now };
        var deletedOrder = new Order { Id = Guid.NewGuid(), FinalAmount = 200_000, IsDeleted = true, CreatedAt = now };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = validOrder.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 50_000 },
            new() { Id = Guid.NewGuid(), OrderId = deletedOrder.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 4, UnitPrice = 50_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { validOrder, deletedOrder });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Equal(1, result.TotalTicketsSold);
        Assert.Equal(50_000m, result.TotalRevenue);
    }

    [Fact(DisplayName = "UTCID10 (A) - Chi tiết đơn không phải vé (ItemType != TICKET) -> Không tính vào số lượng vé bán")]
    public async Task UTCID10_NonTicketItem_IsNotCountedInTicketSold()
    {
        var now = DateTime.UtcNow;
        var order = new Order { Id = Guid.NewGuid(), FinalAmount = 150_000, IsDeleted = false, CreatedAt = now };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = order.Id, ItemId = Guid.NewGuid(), ItemType = "Product", Quantity = 3, UnitPrice = 50_000 } // Đồ uống/thuê phao
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { order });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType>());
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Equal(0, result.TotalTicketsSold); // 0 vé bán ra
        Assert.Equal(150_000m, result.TotalRevenue);
    }

    [Fact(DisplayName = "UTCID11 (N) - Tính giá trị trung bình đơn hàng -> Bằng tổng doanh thu chia số đơn hàng")]
    public async Task UTCID11_AverageOrderValue_CalculatedCorrectly()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000 };

        var o1 = new Order { Id = Guid.NewGuid(), FinalAmount = 100_000, IsDeleted = false, CreatedAt = now };
        var o2 = new Order { Id = Guid.NewGuid(), FinalAmount = 200_000, IsDeleted = false, CreatedAt = now };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = o1.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 2, UnitPrice = 50_000 },
            new() { Id = Guid.NewGuid(), OrderId = o2.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 4, UnitPrice = 50_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { o1, o2 });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Equal(300_000m, result.TotalRevenue);
        Assert.Equal(150_000m, result.AverageOrderValue); // 300.000 / 2 đơn = 150.000
    }

    [Fact(DisplayName = "UTCID12 (N) - Kỳ trước có doanh thu -> Tính đúng tỷ lệ tăng trưởng doanh thu (%)")]
    public async Task UTCID12_GrowthRate_CalculatedAccurately()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000 };

        // Kỳ trước (8-14 ngày trước): 100.000đ
        var prevOrder = new Order { Id = Guid.NewGuid(), FinalAmount = 100_000, IsDeleted = false, CreatedAt = now.AddDays(-10) };
        // Kỳ này (7 ngày qua): 150.000đ (tăng 50%)
        var currentOrder = new Order { Id = Guid.NewGuid(), FinalAmount = 150_000, IsDeleted = false, CreatedAt = now.AddDays(-2) };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = prevOrder.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 2, UnitPrice = 50_000 },
            new() { Id = Guid.NewGuid(), OrderId = currentOrder.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 3, UnitPrice = 50_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { prevOrder, currentOrder });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "7days" }, CancellationToken.None);

        Assert.Equal(50.0, result.RevenueGrowthRate); // ((150k - 100k) / 100k) * 100 = 50%
    }

    [Fact(DisplayName = "UTCID13 (B) - Kỳ trước doanh thu = 0, kỳ này có doanh thu -> Tăng trưởng đạt 100%")]
    public async Task UTCID13_ZeroPreviousRevenue_GrowthRateIs100Percent()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000 };

        var currentOrder = new Order { Id = Guid.NewGuid(), FinalAmount = 100_000, IsDeleted = false, CreatedAt = now };
        var detail = new OrderDetail { Id = Guid.NewGuid(), OrderId = currentOrder.Id, ItemId = tt.Id, ItemType = "TICKET", Quantity = 2, UnitPrice = 50_000 };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { currentOrder });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<OrderDetail> { detail });
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Equal(100.0, result.RevenueGrowthRate);
    }

    [Fact(DisplayName = "UTCID14 (N) - Bảng giao dịch gần nhất -> Hiển thị đúng thông tin khách hàng và thanh toán")]
    public async Task UTCID14_RecentTransactions_PopulatesCustomerAndPaymentInfo()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", Price = 500_000 };
        var orderId = Guid.NewGuid();

        var order = new Order
        {
            Id = orderId,
            CustomerName = "Nguyễn Văn An",
            CustomerPhone = "0987654321",
            FinalAmount = 500_000,
            Status = "COMPLETED",
            IsDeleted = false,
            CreatedAt = now
        };
        var detail = new OrderDetail { Id = Guid.NewGuid(), OrderId = orderId, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 500_000 };
        var payment = new Payment { Id = Guid.NewGuid(), OrderId = orderId, Amount = 500_000, PaymentMethod = "Chuyển khoản", PaymentTime = now };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { order });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<OrderDetail> { detail });
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment> { payment });
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Single(result.RecentTransactions);
        var tx = result.RecentTransactions[0];
        Assert.Equal("Nguyễn Văn An", tx.CustomerName);
        Assert.Equal("0987654321", tx.Phone);
        Assert.Equal("Vé tháng người lớn", tx.TicketType);
        Assert.Equal("Chuyển khoản", tx.PaymentMethod);
        Assert.Equal("COMPLETED", tx.Status);
    }

    [Fact(DisplayName = "UTCID15 (A) - Đơn hàng không có tên khách -> Hiển thị mặc định Khách vãng lai")]
    public async Task UTCID15_OrderWithoutCustomerName_DefaultsToKhachVangLai()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000 };
        var orderId = Guid.NewGuid();

        var order = new Order
        {
            Id = orderId,
            CustomerName = null, // Không nhập tên
            CustomerPhone = null,
            FinalAmount = 50_000,
            Status = "COMPLETED",
            IsDeleted = false,
            CreatedAt = now
        };
        var detail = new OrderDetail { Id = Guid.NewGuid(), OrderId = orderId, ItemId = tt.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 50_000 };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { order });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<OrderDetail> { detail });
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTicketRevenueStatisticsQuery { Period = "today" }, CancellationToken.None);

        Assert.Single(result.RecentTransactions);
        Assert.Equal("Khách vãng lai", result.RecentTransactions[0].CustomerName);
    }

    [Fact(DisplayName = "UTCID16 (N) - Lọc theo vé lượt (TicketCategory = 'VE_LUOT') -> Chỉ tính doanh thu và số lượng của vé lượt")]
    public async Task UTCID16_FilterBySingleTicketCategory_OnlyCalculatesSingleTickets()
    {
        var now = DateTime.UtcNow;
        var ttSingle = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000, TicketCategory = "VE_LUOT" };
        var ttMonthly = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", Price = 500_000, TicketCategory = "VE_THANG" };

        var o1 = new Order { Id = Guid.NewGuid(), FinalAmount = 50_000, IsDeleted = false, CreatedAt = now };
        var o2 = new Order { Id = Guid.NewGuid(), FinalAmount = 500_000, IsDeleted = false, CreatedAt = now };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = o1.Id, ItemId = ttSingle.Id, ItemType = "TICKET", Quantity = 2, UnitPrice = 50_000 },
            new() { Id = Guid.NewGuid(), OrderId = o2.Id, ItemId = ttMonthly.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 500_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { o1, o2 });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { ttSingle, ttMonthly });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var query = new GetTicketRevenueStatisticsQuery { Period = "today", TicketCategory = "VE_LUOT" };
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.TotalTicketsSold);
        Assert.Equal(100_000m, result.TotalRevenue);
        Assert.All(result.TicketTypeRevenues, r => Assert.Contains("lượt", r.Name.ToLowerInvariant()));
    }

    [Fact(DisplayName = "UTCID17 (N) - Lọc theo vé tháng (TicketCategory = 'VE_THANG') -> Chỉ tính doanh thu và số lượng của vé tháng")]
    public async Task UTCID17_FilterByMonthlyTicketCategory_OnlyCalculatesMonthlyTickets()
    {
        var now = DateTime.UtcNow;
        var ttSingle = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", Price = 50_000, TicketCategory = "VE_LUOT" };
        var ttMonthly = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", Price = 500_000, TicketCategory = "VE_THANG" };

        var o1 = new Order { Id = Guid.NewGuid(), FinalAmount = 50_000, IsDeleted = false, CreatedAt = now };
        var o2 = new Order { Id = Guid.NewGuid(), FinalAmount = 500_000, IsDeleted = false, CreatedAt = now };

        var details = new List<OrderDetail>
        {
            new() { Id = Guid.NewGuid(), OrderId = o1.Id, ItemId = ttSingle.Id, ItemType = "TICKET", Quantity = 2, UnitPrice = 50_000 },
            new() { Id = Guid.NewGuid(), OrderId = o2.Id, ItemId = ttMonthly.Id, ItemType = "TICKET", Quantity = 1, UnitPrice = 500_000 }
        };

        _orderRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order> { o1, o2 });
        _orderDetailRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _paymentRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>());
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { ttSingle, ttMonthly });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());

        var handler = CreateHandler();
        var query = new GetTicketRevenueStatisticsQuery { Period = "today", TicketCategory = "VE_THANG" };
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(1, result.TotalTicketsSold);
        Assert.Equal(500_000m, result.TotalRevenue);
        Assert.All(result.TicketTypeRevenues, r => Assert.Contains("tháng", r.Name.ToLowerInvariant()));
    }
}

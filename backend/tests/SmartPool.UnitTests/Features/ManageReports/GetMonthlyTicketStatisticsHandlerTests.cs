using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SmartPool.Application.Features.ManageReports.Queries.GetMonthlyTicketStatistics;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageReports;

public sealed class GetMonthlyTicketStatisticsHandlerTests
{
    private readonly Mock<IRepository<Ticket>> _ticketRepo = new();
    private readonly Mock<IRepository<TicketType>> _ticketTypeRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IRepository<UserProfile>> _profileRepo = new();
    private readonly Mock<IRepository<EntryLog>> _entryLogRepo = new();

    private GetMonthlyTicketStatisticsHandler CreateHandler()
    {
        return new GetMonthlyTicketStatisticsHandler(
            _ticketRepo.Object,
            _ticketTypeRepo.Object,
            _userRepo.Object,
            _profileRepo.Object,
            _entryLogRepo.Object);
    }

    [Fact(DisplayName = "UTCID01 (N) - Tính toán toàn diện bộ 4 KPI & Tỷ lệ gia hạn với dữ liệu thực tế")]
    public async Task UTCID01_ValidMonthlyTickets_CalculatesAllKpisAndRenewalRate()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType
        {
            Id = Guid.NewGuid(),
            Name = "Vé tháng người lớn",
            TicketCategory = TicketCategoryEnum.VE_THANG.ToString()
        };

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var tickets = new List<Ticket>
        {
            // user1 có 2 vé: 1 vé cũ hết hạn tháng trước, 1 vé mới tạo tháng này đang hoạt động còn 20 ngày
            new() { Id = Guid.NewGuid(), TicketTypeId = tt.Id, UserId = user1, CreatedAt = now.AddMonths(-1), ExpiryDate = now.AddDays(-5), Status = "EXPIRED", IsDeleted = false },
            new() { Id = Guid.NewGuid(), TicketTypeId = tt.Id, UserId = user1, CreatedAt = now, ExpiryDate = now.AddDays(20), Status = "ACTIVE", IsDeleted = false },
            // user2 có 1 vé sắp hết hạn (còn 4 ngày)
            new() { Id = Guid.NewGuid(), TicketTypeId = tt.Id, UserId = user2, CreatedAt = now.AddMonths(-1), ExpiryDate = now.AddDays(4), Status = "ACTIVE", IsDeleted = false },
        };

        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tickets);
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());
        _entryLogRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EntryLog>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "all" }, CancellationToken.None);

        Assert.Equal(2, result.TotalMembers); // 2 khách hàng phân biệt
        Assert.Equal(1, result.NewMembersThisMonth); // 1 vé tạo trong tháng này
        Assert.Equal(2, result.ActiveTickets); // 2 vé còn hạn
        Assert.Equal(100.0, result.ActivePercentage); // 2/2 = 100%
        Assert.Equal(1, result.ExpiringSoonTickets); // 1 vé còn 4 ngày <= 7 ngày
        Assert.Equal(1, result.ExpiredTickets); // 1 vé đã quá hạn
        Assert.Equal(50.0, result.RenewalRate); // 1/2 user có > 1 vé => 50%
    }

    [Fact(DisplayName = "UTCID02 (N) - Biểu đồ xu hướng 6 tháng & Phân bổ cơ cấu 2 gói vé tháng thật")]
    public async Task UTCID02_MonthlyTrendsAndPackageDistributions_CalculatesAccurately()
    {
        var now = DateTime.UtcNow;
        var ttAdult = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", TicketCategory = TicketCategoryEnum.VE_THANG.ToString() };
        var ttChild = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng trẻ em", TicketCategory = TicketCategoryEnum.VE_THANG.ToString() };

        var tickets = new List<Ticket>
        {
            new() { Id = Guid.NewGuid(), TicketTypeId = ttAdult.Id, CreatedAt = now, ExpiryDate = now.AddDays(30), IsDeleted = false },
            new() { Id = Guid.NewGuid(), TicketTypeId = ttAdult.Id, CreatedAt = now, ExpiryDate = now.AddDays(30), IsDeleted = false },
            new() { Id = Guid.NewGuid(), TicketTypeId = ttAdult.Id, CreatedAt = now.AddMonths(-1), ExpiryDate = now.AddDays(10), IsDeleted = false },
            new() { Id = Guid.NewGuid(), TicketTypeId = ttChild.Id, CreatedAt = now, ExpiryDate = now.AddDays(30), IsDeleted = false },
        };

        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tickets);
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { ttAdult, ttChild });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());
        _entryLogRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EntryLog>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "all" }, CancellationToken.None);

        // Xu hướng 6 tháng
        Assert.Equal(6, result.MonthlyTrends.Count);
        var currentMonthTrend = result.MonthlyTrends.Last();
        Assert.Equal(3, currentMonthTrend.NewMembers); // 3 vé tạo trong tháng hiện tại

        // Cơ cấu gói vé: 3 người lớn (75%), 1 trẻ em (25%)
        Assert.Equal(2, result.PackageDistributions.Count);
        var adultPkg = result.PackageDistributions.First(p => p.Name == "Vé tháng người lớn");
        var childPkg = result.PackageDistributions.First(p => p.Name == "Vé tháng trẻ em");

        Assert.Equal(3, adultPkg.Value);
        Assert.Equal(75.0, adultPkg.Percentage);
        Assert.Equal("#8b5cf6", adultPkg.Color);

        Assert.Equal(1, childPkg.Value);
        Assert.Equal(25.0, childPkg.Percentage);
        Assert.Equal("#0284c7", childPkg.Color);
    }

    [Fact(DisplayName = "UTCID03 (N) - Hiển thị chi tiết thông tin hội viên & Đếm số lượt quẹt thẻ từ EntryLog")]
    public async Task UTCID03_MemberDetailsAndEntryLogsCount_PopulatedCorrectly()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", TicketCategory = TicketCategoryEnum.VE_THANG.ToString() };
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            TicketTypeId = tt.Id,
            UserId = userId,
            CreatedAt = now.AddDays(-10),
            IssueDate = now.AddDays(-10),
            ExpiryDate = now.AddDays(20),
            IsDeleted = false
        };

        var user = new User { Id = userId, Email = "an.nguyen@example.com", Phone = "0987654321", Username = "annguyen" };
        var profile = new UserProfile { UserId = userId, FullName = "Nguyễn Văn An" };

        var logs = new List<EntryLog>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticket.Id, ScanTime = now.AddDays(-5) },
            new() { Id = Guid.NewGuid(), TicketId = ticket.Id, ScanTime = now.AddDays(-2) },
            new() { Id = Guid.NewGuid(), TicketId = Guid.NewGuid(), ScanTime = now } // Của vé khác
        };

        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket> { ticket });
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User> { user });
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile> { profile });
        _entryLogRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(logs);

        var handler = CreateHandler();
        var result = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "all" }, CancellationToken.None);

        Assert.Single(result.MemberWarnings);
        var member = result.MemberWarnings[0];
        Assert.Equal("Nguyễn Văn An", member.CustomerName);
        Assert.Equal("an.nguyen@example.com", member.Email);
        Assert.Equal("0987654321", member.Phone);
        Assert.Equal("Vé tháng người lớn", member.PackageName);
        Assert.Equal(2, member.TotalEntriesUsed); // Đếm đúng 2 lượt quẹt thẻ của vé này
        Assert.Equal("ACTIVE", member.Status);
    }

    [Fact(DisplayName = "UTCID04 (A) - Xử lý hội viên khuyết thông tin: Không có Profile hoặc UserId null")]
    public async Task UTCID04_MissingUserInfo_FallbacksGracefully()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng trẻ em", TicketCategory = TicketCategoryEnum.VE_THANG.ToString() };
        var userId = Guid.NewGuid();

        var tickets = new List<Ticket>
        {
            // Vé 1: Có User nhưng không có UserProfile -> lấy Username
            new() { Id = Guid.NewGuid(), TicketTypeId = tt.Id, UserId = userId, ExpiryDate = now.AddDays(15), IsDeleted = false },
            // Vé 2: Không có UserId (UserId null) -> hiển thị Khách hàng hội viên mặc định
            new() { Id = Guid.NewGuid(), TicketTypeId = tt.Id, UserId = null, ExpiryDate = now.AddDays(15), IsDeleted = false }
        };

        var user = new User { Id = userId, Email = "swimmer@test.com", Phone = "0911222333", Username = "super_swimmer" };

        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tickets);
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User> { user });
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>()); // Không có Profile
        _entryLogRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EntryLog>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "all" }, CancellationToken.None);

        Assert.Equal(2, result.MemberWarnings.Count);
        var userTicket = result.MemberWarnings.First(m => m.Email == "swimmer@test.com");
        var nullUserTicket = result.MemberWarnings.First(m => m.Email == "hoi-vien@gmail.com");

        Assert.Equal("super_swimmer", userTicket.CustomerName);
        Assert.Equal("0911222333", userTicket.Phone);

        Assert.Equal("Khách hàng hội viên", nullUserTicket.CustomerName);
        Assert.Equal("0901.xxx.xxx", nullUserTicket.Phone);
    }

    [Fact(DisplayName = "UTCID05 (A) - Vé có ExpiryDate hoặc IssueDate null -> Không crash và hiển thị ký tự '—'")]
    public async Task UTCID05_NullDatesOnTicket_HandlesSafelyWithoutException()
    {
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", TicketCategory = TicketCategoryEnum.VE_THANG.ToString() };
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            TicketTypeId = tt.Id,
            CreatedAt = null,
            IssueDate = null,
            ExpiryDate = null,
            Status = "ACTIVE",
            IsDeleted = false
        };

        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket> { ticket });
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());
        _entryLogRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EntryLog>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "all" }, CancellationToken.None);

        Assert.Single(result.MemberWarnings);
        var member = result.MemberWarnings[0];
        Assert.Equal("—", member.IssueDate);
        Assert.Equal("—", member.ExpiryDate);
        Assert.Equal(0, member.DaysRemaining);
    }

    [Fact(DisplayName = "UTCID06 (N) - Lọc danh sách theo bộ lọc: Mặc định (null), active, expired và all")]
    public async Task UTCID06_FilterParam_AppliesCorrectFilteringCriteria()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", TicketCategory = TicketCategoryEnum.VE_THANG.ToString() };

        var tickets = new List<Ticket>
        {
            new() { Id = Guid.NewGuid(), TicketTypeId = tt.Id, ExpiryDate = now.AddDays(25), Status = "ACTIVE", IsDeleted = false }, // ACTIVE
            new() { Id = Guid.NewGuid(), TicketTypeId = tt.Id, ExpiryDate = now.AddDays(3), Status = "ACTIVE", IsDeleted = false },  // EXPIRING_SOON
            new() { Id = Guid.NewGuid(), TicketTypeId = tt.Id, ExpiryDate = now.AddDays(-2), Status = "EXPIRED", IsDeleted = false }  // EXPIRED
        };

        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tickets);
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());
        _entryLogRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EntryLog>());

        var handler = CreateHandler();

        // 1. Filter = null -> Mặc định trả về EXPIRING_SOON
        var defaultResult = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = null }, CancellationToken.None);
        Assert.Single(defaultResult.MemberWarnings);
        Assert.Equal("EXPIRING_SOON", defaultResult.MemberWarnings[0].Status);

        // 2. Filter = "active" -> Chỉ trả về ACTIVE (> 7 ngày)
        var activeResult = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "active" }, CancellationToken.None);
        Assert.Single(activeResult.MemberWarnings);
        Assert.Equal("ACTIVE", activeResult.MemberWarnings[0].Status);

        // 3. Filter = "expired" -> Chỉ trả về EXPIRED (< 0 ngày)
        var expiredResult = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "expired" }, CancellationToken.None);
        Assert.Single(expiredResult.MemberWarnings);
        Assert.Equal("EXPIRED", expiredResult.MemberWarnings[0].Status);

        // 4. Filter = "all" -> Trả về tất cả 3 vé
        var allResult = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "all" }, CancellationToken.None);
        Assert.Equal(3, allResult.MemberWarnings.Count);
    }

    [Fact(DisplayName = "UTCID07 (B) - Kiểm tra ngưỡng biên cảnh báo 7 ngày (7 ngày, 8 ngày, quá hạn 1 ngày)")]
    public async Task UTCID07_Boundary7Days_ClassifiesStatusCorrectly()
    {
        var now = DateTime.UtcNow;
        var tt = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", TicketCategory = TicketCategoryEnum.VE_THANG.ToString() };

        var ticketBoundary7Days = new Ticket { Id = Guid.NewGuid(), TicketTypeId = tt.Id, ExpiryDate = now.AddDays(7), IsDeleted = false };
        var ticketBoundary8Days = new Ticket { Id = Guid.NewGuid(), TicketTypeId = tt.Id, ExpiryDate = now.AddDays(8), IsDeleted = false };
        var ticketBoundaryExpired1Day = new Ticket { Id = Guid.NewGuid(), TicketTypeId = tt.Id, ExpiryDate = now.AddDays(-1), IsDeleted = false };

        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Ticket>
        {
            ticketBoundary7Days,
            ticketBoundary8Days,
            ticketBoundaryExpired1Day
        });
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { tt });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());
        _entryLogRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EntryLog>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "all" }, CancellationToken.None);

        var member7Days = result.MemberWarnings.First(m => m.Id == ticketBoundary7Days.Id);
        var member8Days = result.MemberWarnings.First(m => m.Id == ticketBoundary8Days.Id);
        var memberExpired1Day = result.MemberWarnings.First(m => m.Id == ticketBoundaryExpired1Day.Id);

        Assert.Equal("EXPIRING_SOON", member7Days.Status);
        Assert.Equal("ACTIVE", member8Days.Status);
        Assert.Equal("EXPIRED", memberExpired1Day.Status);
    }

    [Fact(DisplayName = "UTCID08 (B) - Loại trừ dữ liệu: Vé đã xóa (IsDeleted = true) và Vé lượt (VE_LUOT)")]
    public async Task UTCID08_DeletedAndNonMonthlyTickets_AreExcluded()
    {
        var now = DateTime.UtcNow;
        var ttSingle = new TicketType { Id = Guid.NewGuid(), Name = "Vé lượt người lớn", TicketCategory = TicketCategoryEnum.VE_LUOT.ToString() };
        var ttMonthly = new TicketType { Id = Guid.NewGuid(), Name = "Vé tháng người lớn", TicketCategory = TicketCategoryEnum.VE_THANG.ToString() };

        var tickets = new List<Ticket>
        {
            // 1. Vé tháng hợp lệ
            new() { Id = Guid.NewGuid(), TicketTypeId = ttMonthly.Id, ExpiryDate = now.AddDays(20), IsDeleted = false },
            // 2. Vé tháng nhưng đã xóa (IsDeleted = true) -> Phải loại bỏ
            new() { Id = Guid.NewGuid(), TicketTypeId = ttMonthly.Id, ExpiryDate = now.AddDays(20), IsDeleted = true },
            // 3. Vé lượt (VE_LUOT) -> Phải loại bỏ
            new() { Id = Guid.NewGuid(), TicketTypeId = ttSingle.Id, ExpiryDate = now.AddDays(1), IsDeleted = false },
        };

        _ticketRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tickets);
        _ticketTypeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TicketType> { ttSingle, ttMonthly });
        _userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _profileRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());
        _entryLogRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EntryLog>());

        var handler = CreateHandler();
        var result = await handler.Handle(new GetMonthlyTicketStatisticsQuery { Filter = "all" }, CancellationToken.None);

        Assert.Equal(1, result.TotalMembers);
        Assert.Single(result.MemberWarnings);
        Assert.Equal("Vé tháng người lớn", result.MemberWarnings[0].PackageName);
    }
}

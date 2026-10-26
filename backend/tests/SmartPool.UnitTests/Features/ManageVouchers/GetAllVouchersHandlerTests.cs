using System.Linq.Expressions;
using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageVouchers.Mapping;
using SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Vouchers;

namespace SmartPool.UnitTests.Features.ManageVouchers;

public class GetAllVouchersHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 5, 0, 0, DateTimeKind.Utc);
    private static readonly Voucher[] Data =
    [
        Create("WELCOME", true, null, null, Now.AddDays(-5)),
        Create("GIAM20K", true, null, Now.AddDays(-1), Now.AddDays(-4)),
        Create("GIAM30K", false, null, null, Now.AddDays(-3)),
        Create("FUTURE", true, Now.AddDays(1), Now.AddDays(3), Now.AddDays(-2)),
        new() { Id = Guid.NewGuid(), Code = "DELETED", IsActive = true, IsDeleted = true, CreatedAt = Now.AddDays(-1) }
    ];

    [Fact(DisplayName = "UTCID01 (N) - Query mặc định -> Trả danh sách đã phân trang, status và bỏ voucher đã xóa")]
    public async Task UTCID01_DefaultQuery_MapsVouchersAndExcludesDeleted()
    {
        var result = await Execute(new());

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(20, result.PageSize);
        Assert.DoesNotContain(result.Items, item => item.Code == "DELETED");
        Assert.Equal(VoucherValidityPolicy.Scheduled, Assert.Single(result.Items, item => item.Code == "FUTURE").Status);
        Assert.Equal(VoucherValidityPolicy.Expired, Assert.Single(result.Items, item => item.Code == "GIAM20K").Status);
        Assert.Equal(VoucherValidityPolicy.Locked, Assert.Single(result.Items, item => item.Code == "GIAM30K").Status);
        Assert.Equal(VoucherValidityPolicy.Active, Assert.Single(result.Items, item => item.Code == "WELCOME").Status);
    }

    [Fact(DisplayName = "UTCID02 (N) - SearchTerm = GIAM -> Tìm chuỗi con trong Code và đếm trước pagination")]
    public async Task UTCID02_SearchTerm_FiltersByCodeSubstring()
    {
        var result = await Execute(new() { SearchTerm = "GIAM", PageSize = 1 });

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("GIAM30K", result.Items[0].Code);
    }

    [Fact(DisplayName = "UTCID03 (N) - IsActive = true -> Chỉ trả voucher được bật thủ công")]
    public async Task UTCID03_IsActiveTrue_FiltersEnabledVouchers()
    {
        var result = await Execute(new() { IsActive = true, PageSize = 100 });

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, item => Assert.True(item.IsActive));
    }

    [Fact(DisplayName = "UTCID04 (N) - IsActive = false -> Chỉ trả voucher bị khóa thủ công")]
    public async Task UTCID04_IsActiveFalse_FiltersLockedVouchers()
    {
        var result = await Execute(new() { IsActive = false, PageSize = 100 });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("GIAM30K", Assert.Single(result.Items).Code);
    }

    [Fact(DisplayName = "UTCID05 (N) - Status filter -> Lọc ACTIVE, EXPIRED, SCHEDULED và LOCKED theo policy")]
    public async Task UTCID05_StatusFilters_ReturnOnlyMatchingStatus()
    {
        var expected = new Dictionary<string, string>
        {
            [VoucherValidityPolicy.Active] = "WELCOME",
            [VoucherValidityPolicy.Expired] = "GIAM20K",
            [VoucherValidityPolicy.Scheduled] = "FUTURE",
            [VoucherValidityPolicy.Locked] = "GIAM30K"
        };

        foreach (var (status, code) in expected)
        {
            var result = await Execute(new() { Status = status, PageSize = 100 });
            Assert.Equal(1, result.TotalCount);
            var item = Assert.Single(result.Items);
            Assert.Equal(code, item.Code);
            Assert.Equal(status, item.Status);
        }
    }

    [Fact(DisplayName = "UTCID06 (N) - PageIndex 1, PageSize 2 -> Sắp xếp CreatedAt giảm dần và trả tổng trước trang")]
    public async Task UTCID06_FirstPage_IsOrderedAndReportsTotal()
    {
        var result = await Execute(new() { PageIndex = 1, PageSize = 2 });

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(new[] { "FUTURE", "GIAM30K" }, result.Items.Select(item => item.Code));
        Assert.Equal(1, result.PageIndex);
    }

    [Fact(DisplayName = "UTCID07 (N) - PageIndex 2, PageSize 2 -> Trả đúng trang tiếp theo")]
    public async Task UTCID07_SecondPage_ReturnsNextRows()
    {
        var result = await Execute(new() { PageIndex = 2, PageSize = 2 });

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(new[] { "GIAM20K", "WELCOME" }, result.Items.Select(item => item.Code));
        Assert.Equal(2, result.PageIndex);
    }

    [Fact(DisplayName = "UTCID08 (B) - PageIndex vượt tổng số trang -> Items rỗng nhưng TotalCount giữ nguyên")]
    public async Task UTCID08_PageBeyondLast_ReturnsEmptyItemsAndTotal()
    {
        var result = await Execute(new() { PageIndex = 3, PageSize = 2 });

        Assert.Equal(4, result.TotalCount);
        Assert.Empty(result.Items);
        Assert.Equal(3, result.PageIndex);
    }

    [Fact(DisplayName = "UTCID09 (A) - PageIndex = 0 -> ValidationException và không gọi operations")]
    public async Task UTCID09_NonPositivePageIndex_IsRejectedBeforeOperation()
    {
        var operations = new Mock<IVoucherOperations>();
        var handler = new GetAllVouchersHandler(operations.Object, new GetAllVouchersValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new() { PageIndex = 0 }, default));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(GetAllVouchersQuery.PageIndex));
        operations.Verify(x => x.GetAllVouchersAsync(It.IsAny<GetAllVouchersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID10 (A) - PageSize 0 hoặc 101 -> ValidationException và không gọi operations")]
    public async Task UTCID10_PageSizeOutsideAllowedRange_IsRejectedBeforeOperation()
    {
        foreach (var pageSize in new[] { 0, 101 })
        {
            var operations = new Mock<IVoucherOperations>();
            var handler = new GetAllVouchersHandler(operations.Object, new GetAllVouchersValidator());

            var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new() { PageSize = pageSize }, default));

            Assert.Contains(exception.Errors, error => error.PropertyName == nameof(GetAllVouchersQuery.PageSize));
            operations.Verify(x => x.GetAllVouchersAsync(It.IsAny<GetAllVouchersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact(DisplayName = "UTCID11 (A) - Status không nằm trong 4 trạng thái hợp lệ -> ValidationException")]
    public async Task UTCID11_UnsupportedStatus_IsRejectedBeforeOperation()
    {
        var operations = new Mock<IVoucherOperations>();
        var handler = new GetAllVouchersHandler(operations.Object, new GetAllVouchersValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new() { Status = "UNKNOWN" }, default));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(GetAllVouchersQuery.Status));
        operations.Verify(x => x.GetAllVouchersAsync(It.IsAny<GetAllVouchersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task<PagedResponse<GetVouchersResponse>> Execute(GetAllVouchersQuery query)
    {
        var rows = new AsyncQuery<Voucher>(Data);
        var set = new Mock<DbSet<Voucher>>();
        set.As<IQueryable<Voucher>>().Setup(x => x.Provider).Returns(((IQueryable)rows).Provider);
        set.As<IQueryable<Voucher>>().Setup(x => x.Expression).Returns(((IQueryable)rows).Expression);
        set.As<IQueryable<Voucher>>().Setup(x => x.ElementType).Returns(typeof(Voucher));
        set.As<IQueryable<Voucher>>().Setup(x => x.GetEnumerator()).Returns(() => Data.AsEnumerable().GetEnumerator());
        set.As<IAsyncEnumerable<Voucher>>().Setup(x => x.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => rows.GetAsyncEnumerator(token));
        var context = new Mock<SmartPoolDbContext>();
        context.SetupGet(c => c.Vouchers).Returns(set.Object);
        var mapper = new MapperConfiguration(c => c.AddProfile<VoucherMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var operations = new VoucherOperations(context.Object, mapper, new FixedTimeProvider());
        var handler = new GetAllVouchersHandler(operations, new GetAllVouchersValidator());

        return await handler.Handle(query, default);
    }

    private static Voucher Create(string code, bool active, DateTime? start, DateTime? end, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(), Code = code, IsActive = active, StartDate = start, EndDate = end,
        IsDeleted = false, DiscountType = "FIXED_AMOUNT", DiscountValue = 20000, CreatedAt = createdAt
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public AsyncQuery(IEnumerable<T> rows) : base(rows) { }
        public AsyncQuery(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new AsyncProvider(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new AsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    private sealed class AsyncProvider(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
        public IQueryable<T> CreateQuery<T>(Expression expression) => new AsyncQuery<T>(expression);
        public object? Execute(Expression expression) => inner.Execute(expression);
        public T Execute<T>(Expression expression) => inner.Execute<T>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType)
                .Invoke(null, [inner.Execute(expression)])!;
        }
    }

    private sealed class AsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    }
}

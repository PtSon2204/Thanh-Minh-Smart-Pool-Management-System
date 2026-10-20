using System.Linq.Expressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Features.ManageVouchers.Mapping;
using SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.UnitTests.Features.ManageVouchers;

public class VoucherListingValidityTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 5, 0, 0, DateTimeKind.Utc);
    private readonly Voucher[] data =
    [
        Create("CURRENT", true, Now.AddDays(-1), Now.AddDays(1)),
        Create("UNLIMITED", true, null, null),
        Create("GIAM20K", true, Now.AddDays(-17), new DateTime(2026, 10, 9, 17, 0, 0, DateTimeKind.Utc)),
        Create("EXPIRING", true, null, Now),
        Create("FUTURE", true, Now.AddMinutes(1), Now.AddDays(1)),
        Create("LOCKED", false, null, null),
        Create("LOCKEDEXPIRED", false, null, Now.AddDays(-1)),
        new() { Id = Guid.NewGuid(), Code = "DELETED", IsDeleted = true, IsActive = true }
    ];

    [Fact]
    public async Task Listing_ReportsExpiryAndScheduleSeparatelyFromTheManualLock()
    {
        var result = await Execute(new() { PageSize = 100 });

        Assert.Equal(7, result.TotalCount);
        Assert.DoesNotContain(result.Items, item => item.Code == "DELETED");
        var expired = Assert.Single(result.Items, item => item.Code == "GIAM20K");
        Assert.True(expired.IsActive); // Still enabled manually, but no longer usable.
        Assert.Equal(VoucherValidityPolicy.Expired, expired.Status);
        Assert.Equal(VoucherValidityPolicy.Scheduled, Assert.Single(result.Items, i => i.Code == "FUTURE").Status);
        Assert.Equal(VoucherValidityPolicy.Locked, Assert.Single(result.Items, i => i.Code == "LOCKED").Status);
    }

    [Theory]
    [InlineData(VoucherValidityPolicy.Active, 2)]
    [InlineData(VoucherValidityPolicy.Expired, 3)]
    [InlineData(VoucherValidityPolicy.Scheduled, 1)]
    [InlineData(VoucherValidityPolicy.Locked, 1)]
    public async Task StatusFilter_IsAppliedBeforeCountingAndPagination(string status, int count)
    {
        var result = await Execute(new() { Status = status, PageSize = 1 });

        Assert.Equal(count, result.TotalCount);
        Assert.Single(result.Items);
        Assert.All(result.Items, item => Assert.Equal(status, item.Status));
    }

    [Fact]
    public void StatusFilters_AgreeWithDisplayedStatusAndCheckoutEligibility()
    {
        foreach (var voucher in data.Where(v => v.IsDeleted != true))
        {
            var expectedStatus = VoucherValidityPolicy.GetStatus(voucher, Now);
            foreach (var status in new[] { "ACTIVE", "EXPIRED", "SCHEDULED", "LOCKED" })
                Assert.Equal(expectedStatus == status, VoucherValidityPolicy.WithStatus(status, Now).Compile()(voucher));
            Assert.Equal(expectedStatus == VoucherValidityPolicy.Active,
                VoucherValidityPolicy.UsableCodeAt(voucher.Code, Now).Compile()(voucher));
        }
    }

    private async Task<SmartPool.Application.Common.Models.PagedResponse<GetVouchersResponse>> Execute(GetAllVouchersQuery query)
    {
        var rows = new AsyncQuery<Voucher>(data);
        var set = new Mock<DbSet<Voucher>>();
        set.As<IQueryable<Voucher>>().Setup(x => x.Provider).Returns(((IQueryable)rows).Provider);
        set.As<IQueryable<Voucher>>().Setup(x => x.Expression).Returns(((IQueryable)rows).Expression);
        set.As<IQueryable<Voucher>>().Setup(x => x.ElementType).Returns(typeof(Voucher));
        set.As<IQueryable<Voucher>>().Setup(x => x.GetEnumerator()).Returns(() => data.AsEnumerable().GetEnumerator());
        set.As<IAsyncEnumerable<Voucher>>().Setup(x => x.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => rows.GetAsyncEnumerator(token));
        var context = new Mock<SmartPoolDbContext>();
        context.SetupGet(c => c.Vouchers).Returns(set.Object);
        var mapper = new MapperConfiguration(c => c.AddProfile<VoucherMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var operations = new VoucherOperations(context.Object, mapper, new FixedTimeProvider());

        return await operations.GetAllVouchersAsync(query, default);
    }

    private static Voucher Create(string code, bool active, DateTime? start, DateTime? end) => new()
    {
        Id = Guid.NewGuid(), Code = code, IsActive = active, StartDate = start, EndDate = end,
        IsDeleted = false, DiscountType = "FIXED_AMOUNT", DiscountValue = 20000, CreatedAt = Now
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    // Execute the real query and pagination in memory without requiring a running database.
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

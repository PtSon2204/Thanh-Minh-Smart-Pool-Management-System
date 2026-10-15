using System.Linq.Expressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.Application.Features.ManageTickets.TicketType.Mapping;
using SmartPool.Application.Interfaces.Repositories;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.TicketType;

internal static class TicketTypeTestSupport
{
    public static IMapper Mapper() => new MapperConfiguration(
        config => config.AddProfile<TicketTypeMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

    public static TicketTypeEntity Entity(string name = "Vé tháng", string category = "VE_THANG",
        bool? active = true, bool? deleted = false, int day = 1) => new()
    {
        Id = Guid.NewGuid(), Name = name, TicketCategory = category, Price = 100000,
        DurationDays = 30, IsActive = active, IsDeleted = deleted,
        CreatedAt = new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc)
    };

    // Evaluate the handler's actual predicate so tests detect missing Id/soft-delete conditions.
    public static Mock<IRepository<TicketTypeEntity>> Repository(params TicketTypeEntity[] entities)
    {
        var repository = new Mock<IRepository<TicketTypeEntity>>(MockBehavior.Strict);
        repository.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<TicketTypeEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<TicketTypeEntity, bool>> predicate, CancellationToken _) =>
                Task.FromResult(entities.FirstOrDefault(predicate.Compile())));
        return repository;
    }

    // Execute production LINQ expressions over in-memory data without a database/provider dependency.
    // These unit tests cover query semantics, not PostgreSQL translation or collation.
    public static DbSet<TicketTypeEntity> DbSet(IEnumerable<TicketTypeEntity> entities)
    {
        var query = new AsyncQuery<TicketTypeEntity>(entities);
        var set = new Mock<DbSet<TicketTypeEntity>>();
        set.As<IQueryable<TicketTypeEntity>>().Setup(x => x.Provider).Returns(((IQueryable)query).Provider);
        set.As<IQueryable<TicketTypeEntity>>().Setup(x => x.Expression).Returns(((IQueryable)query).Expression);
        set.As<IQueryable<TicketTypeEntity>>().Setup(x => x.ElementType).Returns(((IQueryable)query).ElementType);
        set.As<IQueryable<TicketTypeEntity>>().Setup(x => x.GetEnumerator()).Returns(() => query.AsEnumerable().GetEnumerator());
        set.As<IAsyncEnumerable<TicketTypeEntity>>().Setup(x => x.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => query.GetAsyncEnumerator(token));
        return set.Object;
    }

    private sealed class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public AsyncQuery(IEnumerable<T> items) : base(items) { }
        public AsyncQuery(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new AsyncProvider(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new AsyncEnumerator<T>(this.AsEnumerable().GetEnumerator(), cancellationToken);
    }

    private sealed class AsyncProvider(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
        {
            var type = expression.Type.GetGenericArguments()[0];
            return (IQueryable)Activator.CreateInstance(typeof(AsyncQuery<>).MakeGenericType(type), expression)!;
        }
        public IQueryable<T> CreateQuery<T>(Expression expression) => new AsyncQuery<T>(expression);
        public object? Execute(Expression expression) => inner.Execute(expression);
        public T Execute<T>(Expression expression) => inner.Execute<T>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var result = inner.Execute(expression);
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType).Invoke(null, [result])!;
        }
    }

    private sealed class AsyncEnumerator<T>(IEnumerator<T> inner, CancellationToken token) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync()
        {
            token.ThrowIfCancellationRequested();
            return ValueTask.FromResult(inner.MoveNext());
        }
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    }
}

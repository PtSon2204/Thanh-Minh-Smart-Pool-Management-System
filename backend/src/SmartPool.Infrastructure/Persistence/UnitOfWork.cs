using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartPool.Application.Common.Exceptions;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence
{

    public class UnitOfWork : IUnitOfWork
    {
        private readonly SmartPoolDbContext _context;

        public UnitOfWork(SmartPoolDbContext context)
        {
            _context = context;
        }

        public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            return new UnitOfWorkTransaction(transaction);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation } postgres &&
                UserFieldForConstraint(postgres.ConstraintName) is not null)
            {
                var field = UserFieldForConstraint(((PostgresException)exception.InnerException!).ConstraintName)!;
                throw new UniqueUserFieldException(field, exception);
            }
        }

        private static string? UserFieldForConstraint(string? constraintName) => constraintName switch
        {
            "users_username_key" => "username",
            "users_email_key" => "email",
            "users_phone_key" => "phone",
            _ => null
        };

        public void Dispose()
            => _context.Dispose();

        private sealed class UnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) =>
                transaction.CommitAsync(cancellationToken);

            public ValueTask DisposeAsync() => transaction.DisposeAsync();
        }
    }
}

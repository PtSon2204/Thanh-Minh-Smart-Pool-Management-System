namespace SmartPool.Application.Interfaces.Repositories
{
    public interface IUnitOfWorkTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken = default);
    }

    public interface IUnitOfWork : IDisposable
    {
        Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
 

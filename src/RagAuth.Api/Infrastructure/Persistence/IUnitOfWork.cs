namespace RagAuth.Api.Infrastructure.Persistence;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}

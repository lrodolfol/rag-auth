using Microsoft.EntityFrameworkCore.Storage;

namespace RagAuth.Api.Infrastructure.Persistence;

public sealed class EntityFrameworkUnitOfWork(AppDbContext databaseContext) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        databaseContext.SaveChangesAsync(cancellationToken);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);
        return new EntityFrameworkUnitOfWorkTransaction(transaction);
    }

    private sealed class EntityFrameworkUnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

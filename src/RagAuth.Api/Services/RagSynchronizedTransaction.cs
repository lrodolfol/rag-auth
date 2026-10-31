using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Observability;

namespace RagAuth.Api.Services;

public sealed record RagSynchronizedOperation(
    string Name,
    int ClientId,
    Func<CancellationToken, Task> CallRagApiAsync,
    Func<Task>? CompensateRagApiAsync = null);

public interface IRagSynchronizedTransaction
{
    Task ExecuteAsync(RagSynchronizedOperation operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Grava as mudanças locais numa transação, chama a API RAG e só confirma depois do 2xx (research R7).
/// </summary>
public sealed class RagSynchronizedTransaction(
    IUnitOfWork unitOfWork,
    ILogger<RagSynchronizedTransaction> logger) : IRagSynchronizedTransaction
{
    public async Task ExecuteAsync(RagSynchronizedOperation operation, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await operation.CallRagApiAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        await CommitAfterRagSuccessAsync(transaction, operation);
    }

    private async Task CommitAfterRagSuccessAsync(IUnitOfWorkTransaction transaction, RagSynchronizedOperation operation)
    {
        try
        {
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            LogMessages.CommitFailedAfterRagSuccess(logger, operation.Name, operation.ClientId, exception);
            await TryCompensateAsync(operation);
            throw;
        }
    }

    private async Task TryCompensateAsync(RagSynchronizedOperation operation)
    {
        if (operation.CompensateRagApiAsync is null)
        {
            return;
        }

        try
        {
            await operation.CompensateRagApiAsync();
        }
        catch (Exception exception)
        {
            LogMessages.RagCompensationFailed(logger, operation.Name, operation.ClientId, exception);
        }
    }
}

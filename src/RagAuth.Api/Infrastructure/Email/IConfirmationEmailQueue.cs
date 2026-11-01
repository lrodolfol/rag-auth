using RagAuth.Api.Services.EmailConfirmation;

namespace RagAuth.Api.Infrastructure.Email;

public interface IConfirmationEmailQueue
{
    /// <summary>Enfileira sem bloquear; devolve <c>false</c> se a fila estiver cheia.</summary>
    bool TryEnqueue(ConfirmationEmailMessage message);

    IAsyncEnumerable<ConfirmationEmailMessage> ReadAllAsync(CancellationToken cancellationToken);
}

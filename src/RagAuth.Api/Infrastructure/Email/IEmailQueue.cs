namespace RagAuth.Api.Infrastructure.Email;

public interface IEmailQueue
{
    /// <summary>Enfileira sem bloquear; devolve <c>false</c> se a fila estiver cheia.</summary>
    bool TryEnqueue(OutgoingEmailMessage message);

    IAsyncEnumerable<OutgoingEmailMessage> ReadAllAsync(CancellationToken cancellationToken);
}

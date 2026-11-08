using System.Threading.Channels;
using RagAuth.Api.Services.EmailConfirmation;

namespace RagAuth.Api.Infrastructure.Email;

/// <summary>Fila em memória (research R6); mensagens pendentes se perdem no reinício, como aceito na spec.</summary>
public sealed class ChannelConfirmationEmailQueue : IConfirmationEmailQueue
{
    public const int Capacity = 1000;

    private readonly Channel<ConfirmationEmailMessage> _channel = Channel.CreateBounded<ConfirmationEmailMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    public int PendingCount => _channel.Reader.Count;

    public bool TryEnqueue(ConfirmationEmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<ConfirmationEmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

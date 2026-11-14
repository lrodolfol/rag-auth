using System.Threading.Channels;

namespace RagAuth.Api.Infrastructure.Email;

/// <summary>Fila em memória (research R6 da feature 002); mensagens pendentes se perdem no reinício, como aceito na spec.</summary>
public sealed class ChannelEmailQueue : IEmailQueue
{
    public const int Capacity = 1000;

    private readonly Channel<OutgoingEmailMessage> _channel = Channel.CreateBounded<OutgoingEmailMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    public int PendingCount => _channel.Reader.Count;

    public bool TryEnqueue(OutgoingEmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<OutgoingEmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

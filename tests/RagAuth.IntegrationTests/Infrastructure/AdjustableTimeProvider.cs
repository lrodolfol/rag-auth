namespace RagAuth.IntegrationTests.Infrastructure;

/// <summary>
/// Relógio real acrescido de um deslocamento ajustável: os testes avançam o tempo (expiração, limites)
/// sem congelar o relógio da aplicação. Os timers continuam reais (atrasos zerados na configuração).
/// </summary>
public sealed class AdjustableTimeProvider : TimeProvider
{
    private long _offsetTicks;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public void SetUtcNow(DateTimeOffset utcNow) =>
        Interlocked.Exchange(ref _offsetTicks, (utcNow - System.GetUtcNow()).Ticks);

    public void Advance(TimeSpan duration) => Interlocked.Add(ref _offsetTicks, duration.Ticks);

    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}

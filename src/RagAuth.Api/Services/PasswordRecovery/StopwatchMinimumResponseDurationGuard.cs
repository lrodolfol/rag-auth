using System.Diagnostics;
using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;

namespace RagAuth.Api.Services.PasswordRecovery;

/// <summary>
/// Mede a duração com <see cref="Stopwatch"/> (duração, e não data: exceção à regra do <see cref="TimeProvider"/>,
/// plan decisão 7) e completa a espera até <see cref="EmailOptions.PasswordRecoveryMinimumResponseMilliseconds"/>.
/// </summary>
public sealed class StopwatchMinimumResponseDurationGuard(IOptions<EmailOptions> emailOptions) : IMinimumResponseDurationGuard
{
    public async Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await operation(cancellationToken);
        var remaining = MinimumDuration - stopwatch.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, cancellationToken);
        }
    }

    private TimeSpan MinimumDuration => TimeSpan.FromMilliseconds(emailOptions.Value.PasswordRecoveryMinimumResponseMilliseconds);
}

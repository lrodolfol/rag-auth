using System.Diagnostics;
using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Services.PasswordRecovery;

namespace RagAuth.UnitTests.Services.PasswordRecovery;

public sealed class StopwatchMinimumResponseDurationGuardTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(15);

    [Fact]
    public async Task InstantOperationWaitsAtLeastTheMinimumDuration()
    {
        var stopwatch = Stopwatch.StartNew();

        await CreateGuard(100).RunAsync(_ => Task.CompletedTask, CancellationToken.None);

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(100) - Tolerance, $"Duração: {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task SlowOperationAddsNoExtraWait()
    {
        var stopwatch = Stopwatch.StartNew();

        // Uma espera extra indevida somaria o mínimo inteiro (>= 1100 ms).
        await CreateGuard(500).RunAsync(cancellationToken => Task.Delay(600, cancellationToken), CancellationToken.None);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(600 + 500), $"Duração: {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task ZeroMinimumDoesNotWait()
    {
        var stopwatch = Stopwatch.StartNew();

        await CreateGuard(0).RunAsync(_ => Task.CompletedTask, CancellationToken.None);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100), $"Duração: {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task OperationExceptionIsPropagated()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateGuard(10).RunAsync(_ => throw new InvalidOperationException("falha"), CancellationToken.None));
    }

    private static StopwatchMinimumResponseDurationGuard CreateGuard(int minimumMilliseconds) =>
        new(Options.Create(new EmailOptions { PasswordRecoveryMinimumResponseMilliseconds = minimumMilliseconds }));
}

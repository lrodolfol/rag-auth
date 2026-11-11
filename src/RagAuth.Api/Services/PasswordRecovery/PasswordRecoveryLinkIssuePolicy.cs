using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Infrastructure.Persistence.Repositories;

namespace RagAuth.Api.Services.PasswordRecovery;

/// <summary>
/// Limites de emissão da recuperação (FR-005, research R8): intervalo mínimo desde o último link e
/// máximo de links numa janela móvel de 24 horas. Independentes dos limites da confirmação de e-mail.
/// </summary>
public sealed class PasswordRecoveryLinkIssuePolicy(
    IPasswordRecoveryLinkRepository linkRepository,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider) : IPasswordRecoveryLinkIssuePolicy
{
    public static readonly TimeSpan DailyWindow = TimeSpan.FromHours(24);

    public async Task<bool> CanIssueAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return await HasMinimumIntervalElapsedAsync(clientId, now, cancellationToken)
            && await IsUnderDailyLimitAsync(clientId, now, cancellationToken);
    }

    private async Task<bool> HasMinimumIntervalElapsedAsync(int clientId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var latestIssuedAt = await linkRepository.FindLatestIssuedAtAsync(clientId, cancellationToken);
        var minimumInterval = TimeSpan.FromMinutes(emailOptions.Value.PasswordRecoveryIntervalMinutes);
        return latestIssuedAt is null || now - latestIssuedAt.Value >= minimumInterval;
    }

    // Todos os links contam: na recuperação não existe link "de cadastro".
    private async Task<bool> IsUnderDailyLimitAsync(int clientId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var issuedInWindow = await linkRepository.CountIssuedSinceAsync(clientId, now - DailyWindow, cancellationToken);
        return issuedInWindow < emailOptions.Value.MaximumPasswordRecoveryLinksPerDay;
    }
}

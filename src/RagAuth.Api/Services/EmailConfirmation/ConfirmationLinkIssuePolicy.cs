using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence.Repositories;

namespace RagAuth.Api.Services.EmailConfirmation;

public interface IConfirmationLinkIssuePolicy
{
    Task<bool> CanIssueAsync(int clientId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Limites de novo link (FR-015, research R5): intervalo mínimo desde o último link de qualquer origem
/// e máximo de links <c>new-link-request</c> numa janela móvel de 24 horas.
/// </summary>
public sealed class ConfirmationLinkIssuePolicy(
    IEmailConfirmationLinkRepository linkRepository,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider) : IConfirmationLinkIssuePolicy
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
        var minimumInterval = TimeSpan.FromMinutes(emailOptions.Value.NewLinkIntervalMinutes);
        return latestIssuedAt is null || now - latestIssuedAt.Value >= minimumInterval;
    }

    private async Task<bool> IsUnderDailyLimitAsync(int clientId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var issuedInWindow = await linkRepository.CountIssuedSinceAsync(
            clientId,
            ConfirmationLinkOrigin.NewLinkRequest,
            now - DailyWindow,
            cancellationToken);
        return issuedInWindow < emailOptions.Value.MaximumNewLinksPerDay;
    }
}

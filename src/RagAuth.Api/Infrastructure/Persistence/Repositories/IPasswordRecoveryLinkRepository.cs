using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Repositories;

public interface IPasswordRecoveryLinkRepository
{
    void Add(PasswordRecoveryLink link);

    Task<PasswordRecoveryLink?> FindBySelectorWithClientAsync(string selector, CancellationToken cancellationToken = default);

    Task<PasswordRecoveryLink?> FindActiveForClientAsync(int clientId, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> FindLatestIssuedAtAsync(int clientId, CancellationToken cancellationToken = default);

    Task<int> CountIssuedSinceAsync(int clientId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>Relê <c>usedAt</c> e <c>revokedAt</c> do banco, depois do bloqueio da linha do cliente (research R9).</summary>
    Task ReloadAsync(PasswordRecoveryLink link, CancellationToken cancellationToken = default);
}

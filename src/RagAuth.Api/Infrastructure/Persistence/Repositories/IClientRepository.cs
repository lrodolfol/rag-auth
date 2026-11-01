using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Repositories;

public interface IClientRepository
{
    Task<Client?> FindActiveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<Client?> FindPendingByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<Client?> FindActiveByIdAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>Bloqueia a linha do cliente até o fim da transação atual (serializa emissões concorrentes).</summary>
    Task LockForUpdateAsync(int clientId, CancellationToken cancellationToken = default);

    void Add(Client client);
}

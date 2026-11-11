using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Repositories;

public interface IClientRepository
{
    Task<Client?> FindActiveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<Client?> FindPendingByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>Conta não removida e com e-mail confirmado: a única elegível à recuperação de senha.</summary>
    Task<Client?> FindConfirmedByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<Client?> FindActiveByIdAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>Bloqueia a linha do cliente até o fim da transação atual (serializa emissões concorrentes).</summary>
    Task LockForUpdateAsync(int clientId, CancellationToken cancellationToken = default);

    void Add(Client client);
}

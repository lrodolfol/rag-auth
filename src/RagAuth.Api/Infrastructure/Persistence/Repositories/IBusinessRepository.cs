using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Repositories;

public interface IBusinessRepository
{
    Task<Business?> FindByClientIdAsync(int clientId, CancellationToken cancellationToken = default);

    void Add(Business business);

    void Remove(Business business);
}

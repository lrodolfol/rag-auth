using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Repositories;

public sealed class EntityFrameworkClientRepository(AppDbContext databaseContext) : IClientRepository
{
    public Task<Client?> FindActiveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        databaseContext.Clients.FirstOrDefaultAsync(client => client.Email == normalizedEmail, cancellationToken);

    public Task<Client?> FindActiveByIdAsync(int clientId, CancellationToken cancellationToken = default) =>
        databaseContext.Clients.FirstOrDefaultAsync(client => client.Id == clientId, cancellationToken);

    public void Add(Client client) => databaseContext.Clients.Add(client);
}

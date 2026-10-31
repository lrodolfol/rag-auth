using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Repositories;

public sealed class EntityFrameworkBusinessRepository(AppDbContext databaseContext) : IBusinessRepository
{
    public Task<Business?> FindByClientIdAsync(int clientId, CancellationToken cancellationToken = default) =>
        databaseContext.Businesses.FirstOrDefaultAsync(business => business.ClientId == clientId, cancellationToken);

    public void Add(Business business) => databaseContext.Businesses.Add(business);

    public void Remove(Business business) => databaseContext.Businesses.Remove(business);
}

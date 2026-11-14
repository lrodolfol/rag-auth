using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Repositories;

public sealed class EntityFrameworkPasswordRecoveryLinkRepository(AppDbContext databaseContext) : IPasswordRecoveryLinkRepository
{
    public void Add(PasswordRecoveryLink link) => databaseContext.PasswordRecoveryLinks.Add(link);

    public Task<PasswordRecoveryLink?> FindBySelectorWithClientAsync(string selector, CancellationToken cancellationToken = default) =>
        databaseContext.PasswordRecoveryLinks
            .Include(link => link.Client)
            .FirstOrDefaultAsync(link => link.Selector == selector, cancellationToken);

    public Task<PasswordRecoveryLink?> FindActiveForClientAsync(int clientId, CancellationToken cancellationToken = default) =>
        databaseContext.PasswordRecoveryLinks.FirstOrDefaultAsync(
            link => link.ClientId == clientId && link.UsedAt == null && link.RevokedAt == null,
            cancellationToken);

    public Task<DateTimeOffset?> FindLatestIssuedAtAsync(int clientId, CancellationToken cancellationToken = default) =>
        databaseContext.PasswordRecoveryLinks
            .Where(link => link.ClientId == clientId)
            .MaxAsync(link => (DateTimeOffset?)link.IssuedAt, cancellationToken);

    public Task<int> CountIssuedSinceAsync(int clientId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        databaseContext.PasswordRecoveryLinks.CountAsync(
            link => link.ClientId == clientId && link.IssuedAt > since,
            cancellationToken);

    public Task ReloadAsync(PasswordRecoveryLink link, CancellationToken cancellationToken = default) =>
        databaseContext.Entry(link).ReloadAsync(cancellationToken);
}

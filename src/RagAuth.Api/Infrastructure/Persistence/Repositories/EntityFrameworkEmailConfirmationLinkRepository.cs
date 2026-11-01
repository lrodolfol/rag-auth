using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Repositories;

public sealed class EntityFrameworkEmailConfirmationLinkRepository(AppDbContext databaseContext) : IEmailConfirmationLinkRepository
{
    public void Add(EmailConfirmationLink link) => databaseContext.EmailConfirmationLinks.Add(link);

    public Task<EmailConfirmationLink?> FindBySelectorWithClientAsync(string selector, CancellationToken cancellationToken = default) =>
        databaseContext.EmailConfirmationLinks
            .Include(link => link.Client)
            .FirstOrDefaultAsync(link => link.Selector == selector, cancellationToken);

    public Task<EmailConfirmationLink?> FindActiveForClientAsync(int clientId, CancellationToken cancellationToken = default) =>
        databaseContext.EmailConfirmationLinks.FirstOrDefaultAsync(
            link => link.ClientId == clientId && link.UsedAt == null && link.RevokedAt == null,
            cancellationToken);

    public Task<DateTimeOffset?> FindLatestIssuedAtAsync(int clientId, CancellationToken cancellationToken = default) =>
        databaseContext.EmailConfirmationLinks
            .Where(link => link.ClientId == clientId)
            .MaxAsync(link => (DateTimeOffset?)link.IssuedAt, cancellationToken);

    public Task<int> CountIssuedSinceAsync(int clientId, string origin, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        databaseContext.EmailConfirmationLinks.CountAsync(
            link => link.ClientId == clientId && link.Origin == origin && link.IssuedAt > since,
            cancellationToken);
}

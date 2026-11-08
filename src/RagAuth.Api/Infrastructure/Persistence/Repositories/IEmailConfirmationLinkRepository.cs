using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Repositories;

public interface IEmailConfirmationLinkRepository
{
    void Add(EmailConfirmationLink link);

    Task<EmailConfirmationLink?> FindBySelectorWithClientAsync(string selector, CancellationToken cancellationToken = default);

    Task<EmailConfirmationLink?> FindActiveForClientAsync(int clientId, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> FindLatestIssuedAtAsync(int clientId, CancellationToken cancellationToken = default);

    Task<int> CountIssuedSinceAsync(int clientId, string origin, DateTimeOffset since, CancellationToken cancellationToken = default);
}

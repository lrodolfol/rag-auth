using RagAuth.Api.Services.Business;

namespace RagAuth.Api.Infrastructure.RagApi;

public interface IRagApiClient
{
    Task CreateBusinessAsync(string accessCode, NormalizedBusinessData businessData, CancellationToken cancellationToken = default);

    Task UpdateBusinessAsync(string accessCode, NormalizedBusinessData businessData, CancellationToken cancellationToken = default);

    Task DeleteBusinessAsync(string accessCode, CancellationToken cancellationToken = default);
}

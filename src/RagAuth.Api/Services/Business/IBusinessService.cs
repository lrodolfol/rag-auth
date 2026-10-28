using RagAuth.Api.Contracts.Business;

namespace RagAuth.Api.Services.Business;

public interface IBusinessService
{
    Task<BusinessResponse?> FindOwnedBusinessAsync(int clientId, CancellationToken cancellationToken = default);

    Task<BusinessResponse> CreateOwnedBusinessAsync(int clientId, BusinessRequest request, CancellationToken cancellationToken = default);

    Task<BusinessResponse> UpdateOwnedBusinessAsync(int clientId, BusinessRequest request, CancellationToken cancellationToken = default);

    Task DeleteOwnedBusinessAsync(int clientId, CancellationToken cancellationToken = default);
}

using System.Security.Claims;

namespace RagAuth.Api.Services.Clients;

public interface IClientSessionValidator
{
    Task<bool> IsSessionActiveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

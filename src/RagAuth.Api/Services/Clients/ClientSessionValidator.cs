using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Security;

namespace RagAuth.Api.Services.Clients;

public sealed class ClientSessionValidator(IClientRepository clientRepository) : IClientSessionValidator
{
    public async Task<bool> IsSessionActiveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!TryReadClientId(principal, out var clientId))
        {
            return false;
        }

        var client = await clientRepository.FindActiveByIdAsync(clientId, cancellationToken);
        var tokenAccessCode = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return client is not null && string.Equals(client.AccessCode, tokenAccessCode, StringComparison.Ordinal);
    }

    private static bool TryReadClientId(ClaimsPrincipal principal, out int clientId) =>
        int.TryParse(
            principal.FindFirstValue(ClientClaimTypes.ClientId),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out clientId);
}

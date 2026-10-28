using RagAuth.Api.Contracts;
using RagAuth.Api.Security;
using RagAuth.Api.Services.Clients;

namespace RagAuth.Api.Endpoints;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/api/v1/{clientId}", DeleteAccountAsync)
            .RequireAuthorization(AuthorizationPolicies.ClientRouteOwner);
        return endpoints;
    }

    internal static async Task<IResult> DeleteAccountAsync(
        int clientId,
        IClientAccountService clientAccountService,
        CancellationToken cancellationToken)
    {
        await clientAccountService.DeleteAccountAsync(clientId, cancellationToken);
        var response = ApiResponse.Succeeded<object?>(StatusCodes.Status200OK, "Conta removida com sucesso.", null);
        return TypedResults.Json(response, statusCode: StatusCodes.Status200OK);
    }
}

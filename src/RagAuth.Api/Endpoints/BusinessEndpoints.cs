using RagAuth.Api.Contracts;
using RagAuth.Api.Contracts.Business;
using RagAuth.Api.Security;
using RagAuth.Api.Services.Business;

namespace RagAuth.Api.Endpoints;

public static class BusinessEndpoints
{
    public static IEndpointRouteBuilder MapBusinessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var businessGroup = endpoints.MapGroup("/api/v1/{clientId}/business")
            .RequireAuthorization(AuthorizationPolicies.ClientRouteOwner);
        businessGroup.MapPost(string.Empty, CreateAsync);
        businessGroup.MapPut(string.Empty, UpdateAsync);
        businessGroup.MapDelete(string.Empty, DeleteAsync);
        return endpoints;
    }

    internal static async Task<IResult> CreateAsync(
        int clientId,
        BusinessRequest request,
        IBusinessService businessService,
        CancellationToken cancellationToken)
    {
        var business = await businessService.CreateOwnedBusinessAsync(clientId, request, cancellationToken);
        var response = ApiResponse.Succeeded(StatusCodes.Status201Created, "Negócio cadastrado com sucesso.", business);
        return TypedResults.Json(response, statusCode: StatusCodes.Status201Created);
    }

    internal static async Task<IResult> UpdateAsync(
        int clientId,
        BusinessRequest request,
        IBusinessService businessService,
        CancellationToken cancellationToken)
    {
        var business = await businessService.UpdateOwnedBusinessAsync(clientId, request, cancellationToken);
        var response = ApiResponse.Succeeded(StatusCodes.Status200OK, "Negócio atualizado com sucesso.", business);
        return TypedResults.Json(response, statusCode: StatusCodes.Status200OK);
    }

    internal static async Task<IResult> DeleteAsync(
        int clientId,
        IBusinessService businessService,
        CancellationToken cancellationToken)
    {
        await businessService.DeleteOwnedBusinessAsync(clientId, cancellationToken);
        var response = ApiResponse.Succeeded<object?>(StatusCodes.Status200OK, "Negócio removido com sucesso.", null);
        return TypedResults.Json(response, statusCode: StatusCodes.Status200OK);
    }
}

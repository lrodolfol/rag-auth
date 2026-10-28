using Microsoft.AspNetCore.Authorization;

namespace RagAuth.Api.Security;

public sealed class ClientRouteMatchesTokenHandler : AuthorizationHandler<ClientRouteMatchesTokenRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ClientRouteMatchesTokenRequirement requirement)
    {
        var routeClientId = ReadRouteClientId(context.Resource);
        var tokenClientId = context.User.FindFirst(ClientClaimTypes.ClientId)?.Value;
        if (routeClientId is not null && tokenClientId is not null && string.Equals(routeClientId, tokenClientId, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static string? ReadRouteClientId(object? resource) =>
        resource is HttpContext httpContext
            ? httpContext.GetRouteValue(ClientRouteMatchesTokenRequirement.RouteParameterName)?.ToString()
            : null;
}

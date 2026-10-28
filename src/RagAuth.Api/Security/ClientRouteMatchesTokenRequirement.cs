using Microsoft.AspNetCore.Authorization;

namespace RagAuth.Api.Security;

public sealed class ClientRouteMatchesTokenRequirement : IAuthorizationRequirement
{
    public const string RouteParameterName = "clientId";
}

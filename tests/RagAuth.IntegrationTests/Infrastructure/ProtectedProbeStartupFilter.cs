using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RagAuth.Api.Security;

namespace RagAuth.IntegrationTests.Infrastructure;

/// <summary>
/// Mapeia, só nos testes, a rota protegida <c>GET /api/v1/{clientId}/probe</c>. O roteamento extra roda antes do
/// pipeline da aplicação e só define o endpoint; a autenticação, a autorização e a execução continuam no pipeline real.
/// </summary>
public sealed class ProtectedProbeStartupFilter : IStartupFilter
{
    public const string ProbePathTemplate = "/api/v1/{0}/probe";

    private const string EndpointRouteBuilderPropertyKey = "__EndpointRouteBuilder";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => applicationBuilder =>
    {
        applicationBuilder.UseRouting();
        var endpoints = (IEndpointRouteBuilder)applicationBuilder.Properties[EndpointRouteBuilderPropertyKey]!;
        endpoints.MapGet("/api/v1/{clientId}/probe", () => Results.Ok())
            .RequireAuthorization(AuthorizationPolicies.ClientRouteOwner);
        next(applicationBuilder);
    };
}

using RagAuth.Api.Contracts;
using RagAuth.Api.Contracts.Auth;
using RagAuth.Api.Services.Auth;

namespace RagAuth.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var authGroup = endpoints.MapGroup("/api/v1/auth").AllowAnonymous();
        authGroup.MapPost("/register", RegisterAsync);
        authGroup.MapPost("/login", LoginAsync);
        return endpoints;
    }

    internal static async Task<IResult> RegisterAsync(
        RegisterClientRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var registeredClient = await authService.RegisterAsync(request, cancellationToken);
        var response = ApiResponse.Succeeded(StatusCodes.Status201Created, "Cadastro realizado com sucesso.", registeredClient);
        return TypedResults.Json(response, statusCode: StatusCodes.Status201Created);
    }

    internal static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var login = await authService.LoginAsync(request, cancellationToken);
        var response = ApiResponse.Succeeded(StatusCodes.Status200OK, "Login realizado com sucesso.", login);
        return TypedResults.Json(response, statusCode: StatusCodes.Status200OK);
    }
}

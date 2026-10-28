using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Services.Auth;

namespace RagAuth.Api.Services;

public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddSingleton<IAccessCodeGenerator, RandomAccessCodeGenerator>();
        services.AddSingleton<IPasswordHasher<Client>, PasswordHasher<Client>>();
        services.AddSingleton<IPasswordHashingService, IdentityPasswordHashingService>();
        services.AddSingleton<JsonWebTokenHandler>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}

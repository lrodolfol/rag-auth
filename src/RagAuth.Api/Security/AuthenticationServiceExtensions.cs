using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RagAuth.Api.Configuration;
using RagAuth.Api.Services.Clients;

namespace RagAuth.Api.Security;

public static class AuthenticationServiceExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddScoped<IClientSessionValidator, ClientSessionValidator>();
        services.AddScoped<JwtBearerEventsSetup>();
        services.AddSingleton<IAuthorizationHandler, ClientRouteMatchesTokenHandler>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>(ConfigureJwtBearer);
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthorizationPolicies.ClientRouteOwner, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new ClientRouteMatchesTokenRequirement()));
        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions bearerOptions, IOptions<JwtOptions> jwtOptions)
    {
        bearerOptions.MapInboundClaims = false;
        bearerOptions.EventsType = typeof(JwtBearerEventsSetup);
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Value.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
        };
    }
}

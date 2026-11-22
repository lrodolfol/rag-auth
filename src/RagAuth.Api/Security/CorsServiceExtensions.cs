using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;

namespace RagAuth.Api.Security;

public static class CorsServiceExtensions
{
    public const string FrontendPolicyName = "FrontendPolicy";

    private static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "DELETE"];

    public static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<FrontendCorsOptions>>((corsOptions, frontendCorsOptions) =>
                corsOptions.AddPolicy(FrontendPolicyName, policy => BuildFrontendPolicy(policy, frontendCorsOptions.Value)));
        return services;
    }

    private static void BuildFrontendPolicy(CorsPolicyBuilder policy, FrontendCorsOptions frontendCorsOptions) =>
        policy
            .WithOrigins(NormalizeOrigins(frontendCorsOptions.AllowedOrigins))
            .WithMethods(AllowedMethods)
            .AllowAnyHeader();

    private static string[] NormalizeOrigins(IEnumerable<string> origins) =>
        origins.Select(origin => origin.Trim().TrimEnd('/')).ToArray();
}

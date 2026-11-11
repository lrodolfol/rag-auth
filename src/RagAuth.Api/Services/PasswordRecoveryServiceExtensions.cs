using RagAuth.Api.Services.PasswordRecovery;

namespace RagAuth.Api.Services;

public static class PasswordRecoveryServiceExtensions
{
    public static IServiceCollection AddPasswordRecovery(this IServiceCollection services)
    {
        services.AddScoped<IPasswordRecoveryLinkIssuer, PasswordRecoveryLinkIssuer>();
        services.AddScoped<IPasswordRecoveryLinkIssuePolicy, PasswordRecoveryLinkIssuePolicy>();
        services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
        services.AddSingleton<IMinimumResponseDurationGuard, StopwatchMinimumResponseDurationGuard>();
        return services;
    }
}

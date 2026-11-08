using RagAuth.Api.Services.EmailConfirmation;

namespace RagAuth.Api.Services;

public static class EmailConfirmationServiceExtensions
{
    public static IServiceCollection AddEmailConfirmation(this IServiceCollection services)
    {
        services.AddSingleton<IConfirmationLinkGenerator, RandomConfirmationLinkGenerator>();
        services.AddScoped<IConfirmationLinkIssuer, ConfirmationLinkIssuer>();
        services.AddScoped<IConfirmationLinkIssuePolicy, ConfirmationLinkIssuePolicy>();
        services.AddScoped<IEmailConfirmationService, EmailConfirmationService>();
        return services;
    }
}

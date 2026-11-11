using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.Api.Services.SecureLinks;

namespace RagAuth.Api.Services;

public static class EmailConfirmationServiceExtensions
{
    public static IServiceCollection AddEmailConfirmation(this IServiceCollection services)
    {
        services.AddSingleton<ISecureLinkGenerator, RandomSecureLinkGenerator>();
        services.AddScoped<IConfirmationLinkIssuer, ConfirmationLinkIssuer>();
        services.AddScoped<IConfirmationLinkIssuePolicy, ConfirmationLinkIssuePolicy>();
        services.AddScoped<IEmailConfirmationService, EmailConfirmationService>();
        return services;
    }
}

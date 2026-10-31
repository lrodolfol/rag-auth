using RagAuth.Api.Services.Clients;

namespace RagAuth.Api.Services;

public static class ClientServiceExtensions
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        services.AddScoped<IClientAccountService, ClientAccountService>();
        return services;
    }
}

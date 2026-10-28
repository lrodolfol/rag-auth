using RagAuth.Api.Services.Business;

namespace RagAuth.Api.Services;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddSingleton<IBusinessInputNormalizer, BusinessInputNormalizer>();
        services.AddScoped<IRagSynchronizedTransaction, RagSynchronizedTransaction>();
        services.AddScoped<IBusinessService, BusinessService>();
        return services;
    }
}

using RagAuth.Api.Services.Business;
using RagAuth.Api.Services.Dashboard;

namespace RagAuth.Api.Services;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddSingleton<IBusinessInputNormalizer, BusinessInputNormalizer>();
        services.AddScoped<IRagSynchronizedTransaction, RagSynchronizedTransaction>();
        services.AddScoped<IBusinessService, BusinessService>();
        services.AddScoped<IBusinessDashboardService, BusinessDashboardService>();
        return services;
    }
}

namespace RagAuth.Api.Observability;

public static class ObservabilityServiceExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddLogging(logging => logging.AddSimpleConsole(options => options.IncludeScopes = true));
        services.AddHttpContextAccessor();
        services.AddSingleton<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();
        return services;
    }
}

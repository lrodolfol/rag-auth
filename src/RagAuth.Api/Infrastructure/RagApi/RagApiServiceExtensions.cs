using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;

namespace RagAuth.Api.Infrastructure.RagApi;

public static class RagApiServiceExtensions
{
    public static IServiceCollection AddRagApiClient(this IServiceCollection services)
    {
        services.AddSingleton<ICurrentRequestTokenAccessor, HttpContextRequestTokenAccessor>();
        services.AddSingleton<IRagApiResponseTranslator, RagApiResponseTranslator>();
        services.AddTransient<ApiKeyHeaderHandler>();
        services.AddTransient<ClientTokenForwardingHandler>();
        services.AddTransient<CorrelationIdHeaderHandler>();
        services.AddHttpClient<IRagApiClient, RagApiClient>(ConfigureHttpClient)
            .AddHttpMessageHandler<ApiKeyHeaderHandler>()
            .AddHttpMessageHandler<ClientTokenForwardingHandler>()
            .AddHttpMessageHandler<CorrelationIdHeaderHandler>();
        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider serviceProvider, HttpClient httpClient)
    {
        var ragApiOptions = serviceProvider.GetRequiredService<IOptions<RagApiOptions>>().Value;
        httpClient.BaseAddress = new Uri(ragApiOptions.BaseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(ragApiOptions.TimeoutSeconds);
    }
}

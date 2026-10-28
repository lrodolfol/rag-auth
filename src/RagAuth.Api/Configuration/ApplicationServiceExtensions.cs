using RagAuth.Api.Endpoints;
using RagAuth.Api.ErrorHandling;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.Api.Observability;
using RagAuth.Api.Security;
using RagAuth.Api.Services;

namespace RagAuth.Api.Configuration;

public static class ApplicationServiceExtensions
{
    public static WebApplicationBuilder AddApplication(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddApplicationOptions(builder.Configuration)
            .AddPersistence()
            .AddErrorHandling()
            .AddObservability()
            .AddValidation()
            .AddJwtAuthentication()
            .AddAuthServices()
            .AddBusinessServices()
            .AddClientServices()
            .AddRagApiClient();
        return builder;
    }

    public static WebApplication MapApplication(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAuthEndpoints();
        app.MapBusinessEndpoints();
        app.MapClientEndpoints();
        return app;
    }
}

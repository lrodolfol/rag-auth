using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.RagApi;
using Testcontainers.PostgreSql;

namespace RagAuth.IntegrationTests.Infrastructure;

public sealed class RagAuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSigningKey = "chave-de-assinatura-dos-testes-com-mais-de-32-caracteres";
    public const string JwtIssuer = "rag-auth";
    public const string JwtAudience = "rag-api";
    public const string RagApiKey = "chave-secreta-da-api-rag-nos-testes";
    public const string RagApiBaseUrl = "http://rag-api.teste";

    private readonly PostgreSqlContainer _databaseContainer = new PostgreSqlBuilder("postgres:17").Build();

    public LogCaptureProvider LogCapture { get; } = new();

    public FakeRagApiHandler RagApi { get; } = new();

    public async Task InitializeAsync()
    {
        await _databaseContainer.StartAsync();
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await databaseContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE businesses, clients CASCADE");
        LogCapture.Clear();
        RagApi.Reset();
    }

    public async Task ExecuteInDatabaseAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _databaseContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(BuildSettings()));
        builder.ConfigureLogging(logging => logging.AddProvider(LogCapture));
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, ProtectedProbeStartupFilter>();
            services.AddHttpClient<IRagApiClient, RagApiClient>().ConfigurePrimaryHttpMessageHandler(() => RagApi);
        });
    }

    private Dictionary<string, string?> BuildSettings()
    {
        var connection = new NpgsqlConnectionStringBuilder(_databaseContainer.GetConnectionString());
        return new Dictionary<string, string?>
        {
            ["database:host"] = connection.Host,
            ["database:port"] = connection.Port.ToString(CultureInfo.InvariantCulture),
            ["database:name"] = connection.Database,
            ["database:username"] = connection.Username,
            ["DATABASE_PASSWORD"] = connection.Password,
            ["JWT_SIGNING_KEY"] = JwtSigningKey,
            ["RAG_API_KEY"] = RagApiKey,
            ["ragApi:baseUrl"] = RagApiBaseUrl,
            ["ragApi:timeoutSeconds"] = "1",
        };
    }
}

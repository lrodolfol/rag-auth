using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using RagAuth.Api.Infrastructure.Email;
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
    public const string ResendApiKey = "re_chave-secreta-do-resend-nos-testes";
    public const string EmailFromAddress = "nao-responda@ragauth.teste";

    private readonly PostgreSqlContainer _databaseContainer = new PostgreSqlBuilder("postgres:17").Build();

    public LogCaptureProvider LogCapture { get; } = new();

    public FakeRagApiHandler RagApi { get; } = new();

    public FakeConfirmationEmailSender EmailSender { get; } = new();

    public AdjustableTimeProvider Clock { get; } = new();

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
        await WaitForEmailQueueToDrainAsync();
        await databaseContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE email_confirmation_links, businesses, clients CASCADE");
        LogCapture.Clear();
        RagApi.Reset();
        EmailSender.Reset();
        Clock.Reset();
    }

    /// <summary>Evita que e-mails de um teste anterior cheguem ao fake depois do reset.</summary>
    public async Task WaitForEmailQueueToDrainAsync()
    {
        var queue = (ChannelConfirmationEmailQueue)Services.GetRequiredService<IConfirmationEmailQueue>();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (queue.PendingCount > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        await Task.Delay(50);
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
            services.RemoveAll<IConfirmationEmailSender>();
            services.AddSingleton<IConfirmationEmailSender>(EmailSender);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
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
            ["RESEND_API_KEY"] = ResendApiKey,
            ["email:fromAddress"] = EmailFromAddress,
            ["email:retryDelaysSeconds:0"] = "0",
            ["email:retryDelaysSeconds:1"] = "0",
        };
    }
}

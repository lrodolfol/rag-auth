using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

public sealed class StartupConfigurationTests
{
    [Theory]
    [InlineData("DATABASE_PASSWORD")]
    [InlineData("JWT_SIGNING_KEY")]
    [InlineData("RAG_API_KEY")]
    public void ApplicationDoesNotStartWhenSecretIsMissing(string secretName)
    {
        var settings = CreateValidSettings();
        settings[secretName] = string.Empty;
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings)));

        var startupException = Record.Exception(() => factory.CreateClient());

        var validationException = FindOptionsValidationException(startupException);
        Assert.Contains(secretName, validationException.Message);
    }

    private static Dictionary<string, string?> CreateValidSettings() => new()
    {
        ["DATABASE_PASSWORD"] = "senha-de-teste",
        ["JWT_SIGNING_KEY"] = RagAuthApiFactory.JwtSigningKey,
        ["RAG_API_KEY"] = RagAuthApiFactory.RagApiKey,
    };

    private static OptionsValidationException FindOptionsValidationException(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is OptionsValidationException validationException)
            {
                return validationException;
            }

            if (current is AggregateException aggregateException)
            {
                return FindOptionsValidationException(aggregateException.InnerExceptions.First());
            }
        }

        throw new Xunit.Sdk.XunitException($"A inicialização não falhou por validação de configuração: {exception}");
    }
}

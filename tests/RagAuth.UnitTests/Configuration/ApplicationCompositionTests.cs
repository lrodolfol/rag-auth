using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.Api.Services.Auth;
using RagAuth.Api.Services.Business;
using RagAuth.Api.Services.Clients;

namespace RagAuth.UnitTests.Configuration;

public sealed class ApplicationCompositionTests : IAsyncDisposable
{
    private readonly WebApplication _application;

    public ApplicationCompositionTests()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["database:host"] = "localhost",
            ["database:name"] = "rag_auth",
            ["database:username"] = "rag_auth",
            ["DATABASE_PASSWORD"] = "senha",
            ["jwt:issuer"] = "rag-auth",
            ["jwt:audience"] = "rag-api",
            ["JWT_SIGNING_KEY"] = "chave-de-assinatura-dos-testes-com-mais-de-32-caracteres",
            ["ragApi:baseUrl"] = "http://rag-api.teste",
            ["ragApi:timeoutSeconds"] = "7",
            ["RAG_API_KEY"] = "chave-rag",
        });
        builder.AddApplication();
        _application = builder.Build();
        _application.MapApplication();
    }

    [Fact]
    public void ApplicationServicesAreResolvable()
    {
        using var scope = _application.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAuthService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IBusinessService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IClientAccountService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRagApiClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        var databaseContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains("Password=senha", databaseContext.Database.GetConnectionString());
    }

    [Fact]
    public void SecretsComeFromEnvironmentStyleKeys()
    {
        Assert.Equal("senha", _application.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.Password);
        Assert.Equal("chave-rag", _application.Services.GetRequiredService<IOptions<RagApiOptions>>().Value.ApiKey);
        Assert.Equal(7, _application.Services.GetRequiredService<IOptions<RagApiOptions>>().Value.TimeoutSeconds);
    }

    [Fact]
    public void JwtBearerValidatesIssuerAudienceLifetimeAndSignature()
    {
        var bearerOptions = _application.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.False(bearerOptions.MapInboundClaims);
        Assert.Equal("rag-auth", bearerOptions.TokenValidationParameters.ValidIssuer);
        Assert.Equal("rag-api", bearerOptions.TokenValidationParameters.ValidAudience);
        Assert.True(bearerOptions.TokenValidationParameters.ValidateLifetime);
        Assert.Equal(TimeSpan.FromSeconds(30), bearerOptions.TokenValidationParameters.ClockSkew);
    }

    [Fact]
    public async Task AuthorizationRequiresTokenByDefaultAndHasClientRoutePolicy()
    {
        var policyProvider = _application.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.NotNull(await policyProvider.GetFallbackPolicyAsync());
        Assert.NotNull(await policyProvider.GetPolicyAsync("ClientRouteOwner"));
        Assert.NotNull(await _application.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync());
    }

    [Fact]
    public void AllRoutesAreMapped()
    {
        var routes = ((IEndpointRouteBuilder)_application).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText?.TrimEnd('/'))
            .ToList();

        Assert.Contains("/api/v1/auth/register", routes);
        Assert.Contains("/api/v1/auth/login", routes);
        Assert.Contains("/api/v1/{clientId}/business", routes);
        Assert.Contains("/api/v1/{clientId}", routes);
    }

    [Fact]
    public void ProblemDetailsCustomizationTranslatesFrameworkTitlesAndFormatsValidationErrors()
    {
        var customize = _application.Services.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails!;
        var validationProblem = new HttpValidationProblemDetails(new Dictionary<string, string[]> { ["Email"] = ["Inválido."] }) { Status = 400 };
        var domainProblem = new ProblemDetails { Status = 409, Title = "Título do domínio" };

        customize(new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = validationProblem });
        customize(new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = domainProblem, Exception = new BusinessAlreadyExistsException() });

        Assert.Equal("Um ou mais campos são inválidos.", validationProblem.Title);
        Assert.Contains("email", validationProblem.Errors.Keys);
        Assert.Equal("Título do domínio", domainProblem.Title);
    }

    public ValueTask DisposeAsync() => _application.DisposeAsync();
}

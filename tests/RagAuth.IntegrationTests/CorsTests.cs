using System.Net;
using System.Net.Http.Json;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class CorsTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private const string LoginPath = "/api/v1/auth/login";
    private const string RegisterPath = "/api/v1/auth/register";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private const string AllowMethodsHeader = "Access-Control-Allow-Methods";
    private const string AllowHeadersHeader = "Access-Control-Allow-Headers";
    private const string AllowCredentialsHeader = "Access-Control-Allow-Credentials";
    private const string RequestedHeaders = "content-type,authorization,x-qualquer-coisa";
    private const string InvalidCredentialsMessage = "E-mail ou senha inválidos.";
    private const string UnauthenticatedMessage = "Token ausente, inválido ou expirado.";

    private static readonly string ProtectedBusinessPath = $"/api/v1/{Guid.NewGuid()}/business";

    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task PreflightFromFrontendOriginAllowsMethodAndAnyHeader(string method)
    {
        var response = await _httpClient.SendAsync(CreatePreflightRequest(LoginPath, RagAuthApiFactory.FrontendOrigin, method, RequestedHeaders));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(RagAuthApiFactory.FrontendOrigin, ReadHeader(response, AllowOriginHeader));
        Assert.Contains(method, ReadHeader(response, AllowMethodsHeader));
        Assert.All(RequestedHeaders.Split(','), header => Assert.Contains(header, ReadHeader(response, AllowHeadersHeader)));
        Assert.Null(ReadHeader(response, AllowCredentialsHeader));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task PreflightToProtectedRouteDoesNotRequireToken(string method)
    {
        var response = await _httpClient.SendAsync(CreatePreflightRequest(ProtectedBusinessPath, RagAuthApiFactory.FrontendOrigin, method, "authorization"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(RagAuthApiFactory.FrontendOrigin, ReadHeader(response, AllowOriginHeader));
    }

    [Fact]
    public async Task DomainErrorFromFrontendOriginKeepsBodyAndAllowsOrigin()
    {
        var response = await _httpClient.SendAsync(CreateLoginRequest(RagAuthApiFactory.FrontendOrigin));

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Unauthorized, InvalidCredentialsMessage);
        Assert.Equal(RagAuthApiFactory.FrontendOrigin, ReadHeader(response, AllowOriginHeader));
    }

    [Fact]
    public async Task ProtectedRouteWithoutTokenStillRejectsAndAllowsOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, ProtectedBusinessPath);
        request.Headers.Add("Origin", RagAuthApiFactory.FrontendOrigin);

        var response = await _httpClient.SendAsync(request);

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Unauthorized, UnauthenticatedMessage);
        Assert.Equal(RagAuthApiFactory.FrontendOrigin, ReadHeader(response, AllowOriginHeader));
    }

    [Fact]
    public async Task DashboardFromFrontendOriginAllowsOrigin()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var request = TestClientAccounts.CreateRequest(HttpMethod.Get, $"/api/v1/{account.ClientId}/business", account.AccessToken);
        request.Headers.Add("Origin", RagAuthApiFactory.FrontendOrigin);

        var response = await _httpClient.SendAsync(request);

        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.OK);
        Assert.Equal(RagAuthApiFactory.FrontendOrigin, ReadHeader(response, AllowOriginHeader));
    }

    [Fact]
    public async Task ValidationErrorFromFrontendOriginKeepsBodyAndAllowsOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RegisterPath)
        {
            Content = JsonContent.Create(new { name = "Ana", email = "ana@exemplo.com", password = "abc" }),
        };
        request.Headers.Add("Origin", RagAuthApiFactory.FrontendOrigin);

        var response = await _httpClient.SendAsync(request);

        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest, "Um ou mais campos são inválidos.");
        ApiEnvelopeAssertions.AssertFieldError(error, "password");
        Assert.Equal(RagAuthApiFactory.FrontendOrigin, ReadHeader(response, AllowOriginHeader));
    }

    [Fact]
    public async Task UnknownRouteAndUnsupportedMethodFromFrontendOriginAllowOrigin()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var routeRequest = TestClientAccounts.CreateRequest(HttpMethod.Get, "/rota-inexistente", account.AccessToken);
        var methodRequest = TestClientAccounts.CreateRequest(HttpMethod.Get, RegisterPath, account.AccessToken);
        routeRequest.Headers.Add("Origin", RagAuthApiFactory.FrontendOrigin);
        methodRequest.Headers.Add("Origin", RagAuthApiFactory.FrontendOrigin);

        var routeResponse = await _httpClient.SendAsync(routeRequest);
        var methodResponse = await _httpClient.SendAsync(methodRequest);

        await ApiEnvelopeAssertions.AssertErrorAsync(routeResponse, HttpStatusCode.NotFound, "Recurso não encontrado");
        await ApiEnvelopeAssertions.AssertErrorAsync(methodResponse, HttpStatusCode.MethodNotAllowed, "Método não permitido");
        Assert.Equal(RagAuthApiFactory.FrontendOrigin, ReadHeader(routeResponse, AllowOriginHeader));
        Assert.Equal(RagAuthApiFactory.FrontendOrigin, ReadHeader(methodResponse, AllowOriginHeader));
    }

    [Fact]
    public async Task RequestWithoutOriginHasNoCorsHeaders()
    {
        var response = await _httpClient.PostAsJsonAsync(LoginPath, new { email = "naoexiste@exemplo.com", password = "senhaSegura1" });

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Unauthorized, InvalidCredentialsMessage);
        Assert.DoesNotContain(response.Headers, header => header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("https://localhost:4200")]
    [InlineData("http://127.0.0.1:4200")]
    public async Task OtherOriginsAreNotAllowed(string origin)
    {
        var preflightResponse = await _httpClient.SendAsync(CreatePreflightRequest(LoginPath, origin, "POST", RequestedHeaders));
        var loginResponse = await _httpClient.SendAsync(CreateLoginRequest(origin));

        Assert.Null(ReadHeader(preflightResponse, AllowOriginHeader));
        await ApiEnvelopeAssertions.AssertErrorAsync(loginResponse, HttpStatusCode.Unauthorized, InvalidCredentialsMessage);
        Assert.Null(ReadHeader(loginResponse, AllowOriginHeader));
    }

    [Theory]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    public async Task MethodsOutsideAllowedListAreNotAllowed(string method)
    {
        var response = await _httpClient.SendAsync(CreatePreflightRequest(LoginPath, RagAuthApiFactory.FrontendOrigin, method, requestHeaders: null));

        var allowedMethods = ReadHeader(response, AllowMethodsHeader);
        Assert.True(allowedMethods is null || !allowedMethods.Contains(method, StringComparison.OrdinalIgnoreCase));
    }

    private static HttpRequestMessage CreatePreflightRequest(string path, string origin, string method, string? requestHeaders)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        if (requestHeaders is not null)
        {
            request.Headers.Add("Access-Control-Request-Headers", requestHeaders);
        }

        return request;
    }

    private static HttpRequestMessage CreateLoginRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LoginPath)
        {
            Content = JsonContent.Create(new { email = "naoexiste@exemplo.com", password = "senhaSegura1" }),
        };
        request.Headers.Add("Origin", origin);
        return request;
    }

    private static string? ReadHeader(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values) ? string.Join(",", values) : null;
}

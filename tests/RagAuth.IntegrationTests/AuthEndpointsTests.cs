using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuthEndpointsTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private const string RegisterPath = "/api/v1/auth/register";
    private const string LoginPath = "/api/v1/auth/login";
    private const string InvalidCredentialsMessage = "E-mail ou senha inválidos.";

    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisterReturnsCreatedClientWithoutPassword()
    {
        var response = await _httpClient.PostAsJsonAsync(RegisterPath, new { name = "Ana Souza", email = "Ana@Exemplo.com", password = "senhaSegura1" });

        var data = await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Created, "Cadastro realizado com sucesso.");
        Assert.True(data.GetProperty("clientId").GetInt32() > 0);
        Assert.Equal(12, data.GetProperty("accessCode").GetString()!.Length);
        Assert.Equal("ana@exemplo.com", data.GetProperty("email").GetString());
        Assert.False(data.TryGetProperty("password", out _));
        Assert.False(data.TryGetProperty("passwordHash", out _));
        Assert.True(Guid.TryParse(response.Headers.GetValues("X-Correlation-Id").Single(), out _));
    }

    [Fact]
    public async Task RegisterRejectsSameEmailWithDifferentCase()
    {
        await TestClientAccounts.RegisterAndLoginAsync(_httpClient, "ana@exemplo.com");

        var response = await _httpClient.PostAsJsonAsync(RegisterPath, new { name = "Ana", email = "ANA@exemplo.com", password = "senhaSegura1" });

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Conflict, "Já existe uma conta cadastrada com este e-mail.");
    }

    [Fact]
    public async Task RegisterReturnsValidationProblemInPortuguese()
    {
        var response = await _httpClient.PostAsJsonAsync(RegisterPath, new { name = "Ana", email = "ana@exemplo.com", password = "abc" });

        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest, "Um ou mais campos são inválidos.");
        Assert.Equal("Um ou mais campos são inválidos.", error.GetProperty("title").GetString());
        ApiEnvelopeAssertions.AssertFieldError(error, "password");
        Assert.Single(error.GetProperty("errors").GetProperty("password").EnumerateArray());
        Assert.True(error.TryGetProperty("correlationId", out _));
    }

    [Theory]
    [InlineData("{ \"name\": \"Ana\", ")]
    [InlineData("")]
    [InlineData("{ \"name\": 123, \"email\": \"ana@exemplo.com\", \"password\": \"senhaSegura1\" }")]
    public async Task RegisterRejectsMalformedMissingOrMistypedBody(string body)
    {
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(RegisterPath, content);

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnsupportedMethodAndUnknownRouteReturnPortugueseMessages()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);

        var methodResponse = await _httpClient.SendAsync(TestClientAccounts.CreateRequest(HttpMethod.Get, RegisterPath, account.AccessToken));
        var routeResponse = await _httpClient.SendAsync(TestClientAccounts.CreateRequest(HttpMethod.Get, "/rota-inexistente", account.AccessToken));

        await ApiEnvelopeAssertions.AssertErrorAsync(methodResponse, HttpStatusCode.MethodNotAllowed, "Método não permitido");
        await ApiEnvelopeAssertions.AssertErrorAsync(routeResponse, HttpStatusCode.NotFound, "Recurso não encontrado");
    }

    [Fact]
    public async Task ConcurrentRegistrationsWithSameEmailCreateOnlyOneAccount()
    {
        var registrations = Enumerable.Range(0, 2).Select(_ =>
            _httpClient.PostAsJsonAsync(RegisterPath, new { name = "Ana", email = "ana@exemplo.com", password = "senhaSegura1" }));

        var statusCodes = (await Task.WhenAll(registrations)).Select(response => response.StatusCode).Order().ToArray();

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statusCodes);
    }

    [Fact]
    public async Task LoginReturnsTokenAndNullBusiness()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);

        var data = await TestClientAccounts.LoginAsync(_httpClient, "ANA@exemplo.com");

        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("accessToken").GetString()));
        Assert.Equal("Bearer", data.GetProperty("tokenType").GetString());
        Assert.True(data.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
        Assert.Equal(account.ClientId, data.GetProperty("clientId").GetInt32());
        Assert.Equal(account.AccessCode, data.GetProperty("accessCode").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("business").ValueKind);
    }

    [Theory]
    [InlineData("ana@exemplo.com", "senhaErrada9")]
    [InlineData("naoexiste@exemplo.com", "senhaSegura1")]
    public async Task LoginWithWrongPasswordOrUnknownEmailReturnsSameGenericError(string email, string password)
    {
        await TestClientAccounts.RegisterAndLoginAsync(_httpClient);

        var response = await _httpClient.PostAsJsonAsync(LoginPath, new { email, password });

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Unauthorized, InvalidCredentialsMessage);
    }
}

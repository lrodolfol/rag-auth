using System.Net;
using System.Text;
using System.Text.Json;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class BusinessEndpointsTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public static Dictionary<string, object?> CreateValidBusiness() => new()
    {
        ["name"] = "Padaria da Ana",
        ["description"] = "Pães artesanais e cafés.",
        ["email"] = "contato@padariadaana.com",
        ["telephone"] = "(11) 98765-4321",
        ["address"] = "Rua das Flores, 123, Centro, São Paulo",
        ["details"] = "Aberto de segunda a sábado.",
    };

    public static string BusinessPath(int clientId) => $"/api/v1/{clientId}/business";

    [Fact]
    public async Task RequestsWithoutTokenOrWithExpiredTokenAreRejectedWithoutCallingRag()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        var expiredToken = TestTokens.Create(account, expiresAt: DateTime.UtcNow.AddMinutes(-5));

        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            foreach (var token in new[] { null, expiredToken })
            {
                var response = await SendAsync(method, account.ClientId, token, CreateValidBusiness());
                await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Unauthorized);
            }
        }

        Assert.Empty(factory.RagApi.Requests);
    }

    [Theory]
    [InlineData("{ \"name\": ")]
    [InlineData("{ \"name\": 123, \"description\": \"Pães\" }")]
    public async Task MalformedOrMistypedBodyIsRejectedWithoutCallingRag(string body)
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        var request = TestClientAccounts.CreateRequest(HttpMethod.Post, BusinessPath(account.ClientId), account.AccessToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        await ApiEnvelopeAssertions.AssertErrorAsync(await _httpClient.SendAsync(request), HttpStatusCode.BadRequest);
        Assert.Empty(factory.RagApi.Requests);
    }

    [Fact]
    public async Task CreateStoresBusinessAndForwardsCredentialsAndUserCodeToRag()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);

        var response = await SendAsync(HttpMethod.Post, account.ClientId, account.AccessToken, CreateValidBusiness());

        var business = await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Created, "Negócio cadastrado com sucesso.");
        Assert.Equal("11987654321", business.GetProperty("telephone").GetString());
        var ragRequest = Assert.Single(factory.RagApi.Requests);
        Assert.Equal(HttpMethod.Post, ragRequest.Method);
        Assert.Equal("/business", ragRequest.Path);
        Assert.Equal(RagAuthApiFactory.RagApiKey, ragRequest.Headers["x-api-key"]);
        Assert.Equal($"Bearer {account.AccessToken}", ragRequest.Headers["Authorization"]);
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), ragRequest.Headers["X-Correlation-Id"]);
        using var body = JsonDocument.Parse(ragRequest.Body!);
        Assert.Equal(account.AccessCode, body.RootElement.GetProperty("user_code").GetString());
        Assert.Equal("11987654321", body.RootElement.GetProperty("request").GetProperty("telephone").GetString());
    }

    [Fact]
    public async Task SecondCreateIsConflictWithoutCallingRag()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        await SendAsync(HttpMethod.Post, account.ClientId, account.AccessToken, CreateValidBusiness());

        var response = await SendAsync(HttpMethod.Post, account.ClientId, account.AccessToken, CreateValidBusiness());

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Conflict, "Você já possui um negócio cadastrado.");
        Assert.Single(factory.RagApi.Requests);
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("name", "tamanho:151")]
    [InlineData("description", "")]
    [InlineData("description", "tamanho:1001")]
    [InlineData("email", "email-invalido")]
    [InlineData("address", "tamanho:301")]
    [InlineData("address", "Rua A\nNúmero 1")]
    [InlineData("telephone", "987654321")]
    [InlineData("details", "tamanho:2001")]
    public async Task EachViolatedRuleReturnsFieldErrorWithoutCallingRag(string fieldName, string invalidValue)
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        var business = CreateValidBusiness();
        business[fieldName] = invalidValue.StartsWith("tamanho:", StringComparison.Ordinal)
            ? new string('a', int.Parse(invalidValue["tamanho:".Length..]))
            : invalidValue;

        var response = await SendAsync(HttpMethod.Post, account.ClientId, account.AccessToken, business);

        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest);
        ApiEnvelopeAssertions.AssertFieldError(error, fieldName);
        Assert.Empty(factory.RagApi.Requests);
    }

    [Fact]
    public async Task UpdateAndDeleteBeforeCreateAreNotFoundWithoutCallingRag()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);

        var updateResponse = await SendAsync(HttpMethod.Put, account.ClientId, account.AccessToken, CreateValidBusiness());
        var deleteResponse = await SendAsync(HttpMethod.Delete, account.ClientId, account.AccessToken);

        await ApiEnvelopeAssertions.AssertErrorAsync(updateResponse, HttpStatusCode.NotFound, "Não há negócio cadastrado para este cliente.");
        await ApiEnvelopeAssertions.AssertErrorAsync(deleteResponse, HttpStatusCode.NotFound);
        Assert.Empty(factory.RagApi.Requests);
    }

    [Fact]
    public async Task FullLifecycleIsReflectedInLogin()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        await SendAsync(HttpMethod.Post, account.ClientId, account.AccessToken, CreateValidBusiness());
        Assert.Equal("Padaria da Ana", (await TestClientAccounts.LoginAsync(_httpClient)).GetProperty("business").GetProperty("name").GetString());

        var updatedBusiness = CreateValidBusiness();
        updatedBusiness["name"] = "Padaria Nova";
        var updateResponse = await SendAsync(HttpMethod.Put, account.ClientId, account.AccessToken, updatedBusiness);
        await ApiEnvelopeAssertions.AssertSuccessAsync(updateResponse, HttpStatusCode.OK, "Negócio atualizado com sucesso.");
        Assert.Equal("Padaria Nova", (await TestClientAccounts.LoginAsync(_httpClient)).GetProperty("business").GetProperty("name").GetString());

        var deleteResponse = await SendAsync(HttpMethod.Delete, account.ClientId, account.AccessToken);
        var deleteData = await ApiEnvelopeAssertions.AssertSuccessAsync(deleteResponse, HttpStatusCode.OK, "Negócio removido com sucesso.");
        Assert.Equal(JsonValueKind.Null, deleteData.ValueKind);
        Assert.Equal(JsonValueKind.Null, (await TestClientAccounts.LoginAsync(_httpClient)).GetProperty("business").ValueKind);

        var recreateResponse = await SendAsync(HttpMethod.Post, account.ClientId, account.AccessToken, CreateValidBusiness());
        await ApiEnvelopeAssertions.AssertSuccessAsync(recreateResponse, HttpStatusCode.Created);
        Assert.Equal(["POST", "PUT", "DELETE", "POST"], factory.RagApi.Requests.Select(request => request.Method.Method));
    }

    [Fact]
    public async Task RagFailureOnCreateStoresNothing()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        factory.RagApi.RespondWith(HttpStatusCode.InternalServerError);

        await SendAsync(HttpMethod.Post, account.ClientId, account.AccessToken, CreateValidBusiness());

        Assert.Equal(JsonValueKind.Null, (await TestClientAccounts.LoginAsync(_httpClient)).GetProperty("business").ValueKind);
    }

    [Fact]
    public async Task ClientCannotTouchBusinessOfAnotherClient()
    {
        var owner = await TestClientAccounts.RegisterAndLoginAsync(_httpClient, "bia@exemplo.com");
        await SendAsync(HttpMethod.Post, owner.ClientId, owner.AccessToken, CreateValidBusiness());
        var intruder = await TestClientAccounts.RegisterAndLoginAsync(_httpClient, "ana@exemplo.com");

        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            var response = await SendAsync(method, owner.ClientId, intruder.AccessToken, CreateValidBusiness());
            await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Forbidden);
        }

        var ownerLogin = await TestClientAccounts.LoginAsync(_httpClient, "bia@exemplo.com");
        Assert.Equal("Padaria da Ana", ownerLogin.GetProperty("business").GetProperty("name").GetString());
        Assert.Single(factory.RagApi.Requests);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, int clientId, string? accessToken, object? body = null) =>
        _httpClient.SendAsync(TestClientAccounts.CreateRequest(method, BusinessPath(clientId), accessToken, body));
}

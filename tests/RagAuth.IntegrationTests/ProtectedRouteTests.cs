using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ProtectedRouteTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private const string UnauthenticatedMessage = "Token ausente, inválido ou expirado.";
    private const string ForbiddenMessage = "Você não tem permissão para acessar este recurso.";

    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ValidTokenWithOwnClientIdIsAccepted()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);

        var response = await SendProbeAsync(account.ClientId.ToString(), account.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequestWithoutAuthorizationIsRejected()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);

        await AssertUnauthenticatedAsync(await SendProbeAsync(account.ClientId.ToString(), accessToken: null));
    }

    [Fact]
    public async Task AuthorizationWithoutBearerPrefixIsRejected()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        var request = new HttpRequestMessage(HttpMethod.Get, string.Format(ProtectedProbeStartupFilter.ProbePathTemplate, account.ClientId));
        request.Headers.TryAddWithoutValidation("Authorization", account.AccessToken);

        await AssertUnauthenticatedAsync(await _httpClient.SendAsync(request));
    }

    [Fact]
    public async Task TamperedMalformedExpiredOrForeignTokensAreRejected()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        var invalidTokens = new[]
        {
            TestTokens.TamperSignature(account.AccessToken),
            "isto-nao-e-um-jwt",
            TestTokens.Create(account, expiresAt: DateTime.UtcNow.AddMinutes(-5)),
            TestTokens.Create(account, issuer: "outro-emissor"),
            TestTokens.Create(account, audience: "outro-publico"),
            TestTokens.Create(account, signingKey: "outra-chave-de-assinatura-com-mais-de-32-caracteres"),
        };

        foreach (var invalidToken in invalidTokens)
        {
            await AssertUnauthenticatedAsync(await SendProbeAsync(account.ClientId.ToString(), invalidToken));
        }
    }

    [Fact]
    public async Task TokenOfRemovedAccountIsRejected()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        await factory.ExecuteInDatabaseAsync(databaseContext => databaseContext.Clients
            .Where(client => client.Id == account.ClientId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(client => client.DeletedAt, DateTimeOffset.UtcNow)));

        await AssertUnauthenticatedAsync(await SendProbeAsync(account.ClientId.ToString(), account.AccessToken));
    }

    [Theory]
    [InlineData("other")]
    [InlineData("abc")]
    public async Task ClientIdOfAnotherClientOrNonNumericIsForbidden(string routeClientIdKind)
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        var otherAccount = await TestClientAccounts.RegisterAndLoginAsync(_httpClient, "bia@exemplo.com");
        var routeClientId = routeClientIdKind == "other" ? otherAccount.ClientId.ToString() : "abc";

        var response = await SendProbeAsync(routeClientId, account.AccessToken);

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Forbidden, ForbiddenMessage);
        Assert.Contains(factory.LogCapture.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Acesso negado"));
    }

    private async Task AssertUnauthenticatedAsync(HttpResponseMessage response)
    {
        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Unauthorized, UnauthenticatedMessage);
        Assert.Equal("Não autenticado", error.GetProperty("title").GetString());
        Assert.Contains(factory.LogCapture.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Autenticação rejeitada"));
    }

    private Task<HttpResponseMessage> SendProbeAsync(string routeClientId, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, string.Format(ProtectedProbeStartupFilter.ProbePathTemplate, routeClientId));
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return _httpClient.SendAsync(request);
    }
}

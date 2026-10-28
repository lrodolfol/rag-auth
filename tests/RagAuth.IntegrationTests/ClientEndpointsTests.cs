using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ClientEndpointsTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RequestsWithoutTokenOrWithExpiredTokenAreRejected()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        var expiredToken = TestTokens.Create(account, expiresAt: DateTime.UtcNow.AddMinutes(-5));

        foreach (var token in new[] { null, expiredToken })
        {
            await ApiEnvelopeAssertions.AssertErrorAsync(await DeleteAccountAsync(account.ClientId, token), HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task AccountWithoutBusinessIsRemovedWithoutCallingRag()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);

        var response = await DeleteAccountAsync(account.ClientId, account.AccessToken);

        var data = await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.OK, "Conta removida com sucesso.");
        Assert.Equal(JsonValueKind.Null, data.ValueKind);
        Assert.Empty(factory.RagApi.Requests);
    }

    [Fact]
    public async Task AccountWithBusinessIsRemovedAndCredentialsStopWorking()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        await CreateBusinessAsync(account);

        var response = await DeleteAccountAsync(account.ClientId, account.AccessToken);

        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.OK);
        var ragDeletion = factory.RagApi.Requests[^1];
        Assert.Equal(HttpMethod.Delete, ragDeletion.Method);
        Assert.Equal($"{{\"user_code\":\"{account.AccessCode}\"}}", ragDeletion.Body);
        await ApiEnvelopeAssertions.AssertErrorAsync(await DeleteAccountAsync(account.ClientId, account.AccessToken), HttpStatusCode.Unauthorized);
        var loginResponse = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", new { email = account.Email, password = account.Password });
        await ApiEnvelopeAssertions.AssertErrorAsync(loginResponse, HttpStatusCode.Unauthorized, "E-mail ou senha inválidos.");
    }

    [Fact]
    public async Task EmailCanBeReusedButIdAndAccessCodeAreNew()
    {
        var removedAccount = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        await DeleteAccountAsync(removedAccount.ClientId, removedAccount.AccessToken);

        var newAccount = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);

        Assert.NotEqual(removedAccount.ClientId, newAccount.ClientId);
        Assert.NotEqual(removedAccount.AccessCode, newAccount.AccessCode);
    }

    [Fact]
    public async Task ClientIdOfAnotherClientIsForbidden()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        var otherAccount = await TestClientAccounts.RegisterAndLoginAsync(_httpClient, "bia@exemplo.com");

        var response = await DeleteAccountAsync(otherAccount.ClientId, account.AccessToken);

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Forbidden);
        await TestClientAccounts.LoginAsync(_httpClient, "bia@exemplo.com");
    }

    [Fact]
    public async Task RagOutageKeepsAccountActive()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        await CreateBusinessAsync(account);
        factory.RagApi.FailWithConnectionError();

        var response = await DeleteAccountAsync(account.ClientId, account.AccessToken);

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.ServiceUnavailable);
        var login = await TestClientAccounts.LoginAsync(_httpClient, account.Email);
        Assert.Equal("Padaria da Ana", login.GetProperty("business").GetProperty("name").GetString());
    }

    private async Task CreateBusinessAsync(TestClientAccount account)
    {
        var request = TestClientAccounts.CreateRequest(
            HttpMethod.Post,
            BusinessEndpointsTests.BusinessPath(account.ClientId),
            account.AccessToken,
            BusinessEndpointsTests.CreateValidBusiness());
        await ApiEnvelopeAssertions.AssertSuccessAsync(await _httpClient.SendAsync(request), HttpStatusCode.Created);
    }

    private Task<HttpResponseMessage> DeleteAccountAsync(int clientId, string? accessToken) =>
        _httpClient.SendAsync(TestClientAccounts.CreateRequest(HttpMethod.Delete, $"/api/v1/{clientId}", accessToken));
}

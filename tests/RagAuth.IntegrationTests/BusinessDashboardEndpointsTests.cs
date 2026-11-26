using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class BusinessDashboardEndpointsTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private const string BusinessFoundMessage = "Negócio encontrado com sucesso.";
    private const string BusinessMissingMessage = "Nenhum negócio cadastrado. Cadastre o seu negócio para vê-lo no painel.";
    private const string UnauthenticatedMessage = "Token ausente, inválido ou expirado.";

    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ClientWithBusinessReceivesAccountSummaryAndStoredBusiness()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        await CreateBusinessAsync(account);

        var dashboard = await GetDashboardAsync(account, BusinessFoundMessage);

        AssertClientSummary(dashboard.GetProperty("client"), account);
        var storedBusiness = (await TestClientAccounts.LoginAsync(_httpClient)).GetProperty("business");
        Assert.Equal(storedBusiness.GetRawText(), dashboard.GetProperty("business").GetRawText());
        Assert.Equal("Padaria da Ana", dashboard.GetProperty("business").GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdatedBusinessIsReflectedInNextDashboard()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        await CreateBusinessAsync(account);
        var before = (await GetDashboardAsync(account, BusinessFoundMessage)).GetProperty("business");

        var updatedBusiness = BusinessEndpointsTests.CreateValidBusiness();
        updatedBusiness["name"] = "Padaria Nova";
        await SendBusinessAsync(HttpMethod.Put, account, updatedBusiness);
        var after = (await GetDashboardAsync(account, BusinessFoundMessage)).GetProperty("business");

        Assert.Equal("Padaria Nova", after.GetProperty("name").GetString());
        Assert.True(after.GetProperty("updatedAt").GetDateTimeOffset() > before.GetProperty("updatedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task BusinessWithOnlyRequiredFieldsReturnsOptionalFieldsAsNull()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        await SendBusinessAsync(HttpMethod.Post, account, new Dictionary<string, object?> { ["name"] = "Padaria", ["description"] = "Pães" });

        var business = (await GetDashboardAsync(account, BusinessFoundMessage)).GetProperty("business");

        foreach (var optionalField in new[] { "email", "telephone", "address", "details" })
        {
            Assert.Equal(JsonValueKind.Null, business.GetProperty(optionalField).ValueKind);
        }
    }

    [Fact]
    public async Task DashboardNeverExposesSensitiveClientData()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        await CreateBusinessAsync(account);

        var response = await SendDashboardAsync(account.ClientId, account.AccessToken);
        var body = await response.Content.ReadAsStringAsync();
        var client = (await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.OK)).GetProperty("client");

        foreach (var sensitiveField in new[] { "passwordHash", "accessCode", "deletedAt", "emailConfirmedAt", "passwordChangedAt" })
        {
            Assert.False(client.TryGetProperty(sensitiveField, out _), $"Campo sensível exposto: {sensitiveField}");
        }

        Assert.DoesNotContain(account.AccessCode, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepeatedRequestsReturnTheSameData()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        await CreateBusinessAsync(account);

        var first = await GetDashboardAsync(account, BusinessFoundMessage);
        var second = await GetDashboardAsync(account, BusinessFoundMessage);

        Assert.Equal(first.GetRawText(), second.GetRawText());
    }

    [Fact]
    public async Task DashboardDoesNotCallRagAndWorksWhileRagIsDown()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        await CreateBusinessAsync(account);
        var ragRequestsBefore = factory.RagApi.Requests.Count;

        factory.RagApi.RespondWith(HttpStatusCode.InternalServerError);
        await GetDashboardAsync(account, BusinessFoundMessage);
        factory.RagApi.FailWithConnectionError();
        await GetDashboardAsync(account, BusinessFoundMessage);

        Assert.Equal(ragRequestsBefore, factory.RagApi.Requests.Count);
    }

    [Fact]
    public async Task ClientWithoutBusinessReceivesSummaryNullBusinessAndGuidance()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);

        var dashboard = await GetDashboardAsync(account, BusinessMissingMessage);

        AssertClientSummary(dashboard.GetProperty("client"), account);
        Assert.Equal(JsonValueKind.Null, dashboard.GetProperty("business").ValueKind);
    }

    [Fact]
    public async Task ClientThatRemovedBusinessReceivesGuidance()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        await CreateBusinessAsync(account);
        await SendBusinessAsync(HttpMethod.Delete, account);

        var dashboard = await GetDashboardAsync(account, BusinessMissingMessage);

        Assert.Equal(JsonValueKind.Null, dashboard.GetProperty("business").ValueKind);
        Assert.Equal(account.ClientId, dashboard.GetProperty("client").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task MissingInvalidOrExpiredTokenIsUnauthorized()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var invalidTokens = new[]
        {
            null,
            TestTokens.TamperSignature(account.AccessToken),
            TestTokens.Create(account, expiresAt: DateTime.UtcNow.AddMinutes(-5)),
        };

        foreach (var token in invalidTokens)
        {
            var response = await SendDashboardAsync(account.ClientId, token);
            await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Unauthorized, UnauthenticatedMessage);
        }
    }

    [Fact]
    public async Task TokenOfAnotherClientIsForbiddenWithoutLeakingData()
    {
        var owner = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient, "bia@exemplo.com");
        await CreateBusinessAsync(owner);
        var intruder = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient, "ana@exemplo.com");

        var response = await SendDashboardAsync(owner.ClientId, intruder.AccessToken);
        var body = await response.Content.ReadAsStringAsync();

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Forbidden);
        Assert.DoesNotContain("bia@exemplo.com", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Padaria da Ana", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenOfRemovedAccountIsUnauthorized()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        await factory.ExecuteInDatabaseAsync(databaseContext => databaseContext.Clients
            .Where(client => client.Id == account.ClientId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(client => client.DeletedAt, DateTimeOffset.UtcNow)));

        var response = await SendDashboardAsync(account.ClientId, account.AccessToken);

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Unauthorized, UnauthenticatedMessage);
    }

    [Fact]
    public async Task NonNumericClientIdIsRejectedWithoutData()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var request = TestClientAccounts.CreateRequest(HttpMethod.Get, "/api/v1/abc/business", account.AccessToken);

        var response = await _httpClient.SendAsync(request);

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Forbidden);
    }

    private static void AssertClientSummary(JsonElement client, TestClientAccount account)
    {
        Assert.Equal(account.ClientId, client.GetProperty("id").GetInt32());
        Assert.Equal("Ana Souza", client.GetProperty("name").GetString());
        Assert.Equal(account.Email, client.GetProperty("email").GetString());
        Assert.True(client.GetProperty("emailConfirmed").GetBoolean());
        Assert.True(client.GetProperty("createdAt").TryGetDateTimeOffset(out _));
        Assert.True(client.GetProperty("updatedAt").TryGetDateTimeOffset(out _));
    }

    private async Task<JsonElement> GetDashboardAsync(TestClientAccount account, string expectedMessage)
    {
        var response = await SendDashboardAsync(account.ClientId, account.AccessToken);
        return await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.OK, expectedMessage);
    }

    private Task<HttpResponseMessage> SendDashboardAsync(int clientId, string? accessToken) =>
        _httpClient.SendAsync(TestClientAccounts.CreateRequest(HttpMethod.Get, BusinessEndpointsTests.BusinessPath(clientId), accessToken));

    private Task CreateBusinessAsync(TestClientAccount account) =>
        SendBusinessAsync(HttpMethod.Post, account, BusinessEndpointsTests.CreateValidBusiness());

    private async Task SendBusinessAsync(HttpMethod method, TestClientAccount account, object? body = null)
    {
        var request = TestClientAccounts.CreateRequest(method, BusinessEndpointsTests.BusinessPath(account.ClientId), account.AccessToken, body);
        var response = await _httpClient.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"{method} do negócio falhou: {(int)response.StatusCode}");
    }
}

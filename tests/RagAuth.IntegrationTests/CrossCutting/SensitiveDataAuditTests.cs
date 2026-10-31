using System.Net;
using System.Net.Http.Json;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests.CrossCutting;

[Collection(IntegrationTestCollection.Name)]
public sealed class SensitiveDataAuditTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private const string WrongPassword = "senhaErrada9";

    private readonly HttpClient _httpClient = factory.CreateClient();
    private readonly List<string> _errorResponseBodies = [];

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LogsAndErrorResponsesNeverContainPasswordTokenOrApiKey()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        await RecordErrorAsync(await _httpClient.PostAsJsonAsync("/api/v1/auth/login", new { email = account.Email, password = WrongPassword }));
        await RecordErrorAsync(await SendBusinessAsync(HttpMethod.Post, account));
        factory.RagApi.RespondWith(HttpStatusCode.Unauthorized);
        await RecordErrorAsync(await SendBusinessAsync(HttpMethod.Put, account));
        factory.RagApi.RespondWith(HttpStatusCode.InternalServerError);
        await RecordErrorAsync(await SendBusinessAsync(HttpMethod.Put, account));
        factory.RagApi.RespondWith(HttpStatusCode.OK);
        await _httpClient.SendAsync(TestClientAccounts.CreateRequest(HttpMethod.Delete, $"/api/v1/{account.ClientId}", account.AccessToken));
        await RecordErrorAsync(await _httpClient.SendAsync(TestClientAccounts.CreateRequest(HttpMethod.Delete, $"/api/v1/{account.ClientId}", account.AccessToken)));

        var sensitiveValues = new[] { account.Password, WrongPassword, account.AccessToken, RagAuthApiFactory.RagApiKey };
        var auditedTexts = factory.LogCapture.Entries
            .SelectMany(entry => new[] { entry.Message, entry.ExceptionText ?? string.Empty })
            .Concat(_errorResponseBodies)
            .ToList();
        Assert.NotEmpty(factory.LogCapture.Entries);
        foreach (var sensitiveValue in sensitiveValues)
        {
            Assert.DoesNotContain(auditedTexts, text => text.Contains(sensitiveValue, StringComparison.Ordinal));
        }
    }

    private async Task RecordErrorAsync(HttpResponseMessage response)
    {
        Assert.False(response.IsSuccessStatusCode);
        _errorResponseBodies.Add(await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> SendBusinessAsync(HttpMethod method, TestClientAccount account)
    {
        var request = TestClientAccounts.CreateRequest(
            method,
            BusinessEndpointsTests.BusinessPath(account.ClientId),
            account.AccessToken,
            BusinessEndpointsTests.CreateValidBusiness());
        if (method == HttpMethod.Post)
        {
            factory.RagApi.RespondWith(HttpStatusCode.OK);
            return CreateBusinessThenRepeatAsync(request, account);
        }

        return _httpClient.SendAsync(request);
    }

    private async Task<HttpResponseMessage> CreateBusinessThenRepeatAsync(HttpRequestMessage request, TestClientAccount account)
    {
        await _httpClient.SendAsync(request);
        var repeatedRequest = TestClientAccounts.CreateRequest(
            HttpMethod.Post,
            BusinessEndpointsTests.BusinessPath(account.ClientId),
            account.AccessToken,
            BusinessEndpointsTests.CreateValidBusiness());
        return await _httpClient.SendAsync(repeatedRequest);
    }
}

using System.Net;
using System.Net.Http.Json;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests.CrossCutting;

[Collection(IntegrationTestCollection.Name)]
public sealed class CorrelationIdTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EveryResponseHasDistinctCorrelationId()
    {
        var firstResponse = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", new { email = "a@exemplo.com", password = "x" });
        var secondResponse = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", new { email = "a@exemplo.com", password = "x" });

        Assert.NotEqual(ReadCorrelationId(firstResponse), ReadCorrelationId(secondResponse));
    }

    [Fact]
    public async Task CorrelationIdIsSharedByLogsRagCallAndErrorEnvelope()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        factory.LogCapture.Clear();
        factory.RagApi.RespondWith(HttpStatusCode.InternalServerError);
        var request = TestClientAccounts.CreateRequest(
            HttpMethod.Post,
            BusinessEndpointsTests.BusinessPath(account.ClientId),
            account.AccessToken,
            BusinessEndpointsTests.CreateValidBusiness());

        var response = await _httpClient.SendAsync(request);

        var correlationId = ReadCorrelationId(response);
        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadGateway);
        Assert.Equal(correlationId, error.GetProperty("correlationId").GetString());
        Assert.Equal(correlationId, Assert.Single(factory.RagApi.Requests).Headers["X-Correlation-Id"]);
        var applicationLogs = factory.LogCapture.Entries.Where(entry => entry.Category.StartsWith("RagAuth", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(applicationLogs);
        Assert.All(applicationLogs, entry => Assert.Equal(correlationId, entry.CorrelationId));
    }

    private static string ReadCorrelationId(HttpResponseMessage response)
    {
        var correlationId = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.True(Guid.TryParse(correlationId, out _));
        return correlationId;
    }
}

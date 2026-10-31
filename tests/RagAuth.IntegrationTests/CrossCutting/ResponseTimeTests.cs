using System.Diagnostics;
using System.Net.Http.Json;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests.CrossCutting;

[Collection(IntegrationTestCollection.Name)]
public sealed class ResponseTimeTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private const int WarmUpRequestCount = 10;
    private const int MeasuredRequestCount = 100;
    private static readonly TimeSpan MaximumPercentile95 = TimeSpan.FromMilliseconds(500);

    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisterLoginAndTokenValidationStayUnderFiveHundredMillisecondsAtP95()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient, "medicao@exemplo.com");
        var registrationCounter = 0;

        var registerPercentile = await MeasurePercentile95Async(() => _httpClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name = "Cliente", email = $"cliente{Interlocked.Increment(ref registrationCounter)}@exemplo.com", password = "senhaSegura1" }));
        var loginPercentile = await MeasurePercentile95Async(() => _httpClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = account.Email, password = account.Password }));
        var probePercentile = await MeasurePercentile95Async(() => _httpClient.SendAsync(TestClientAccounts.CreateRequest(
            HttpMethod.Get,
            string.Format(ProtectedProbeStartupFilter.ProbePathTemplate, account.ClientId),
            account.AccessToken)));

        Assert.True(registerPercentile < MaximumPercentile95, $"p95 do cadastro: {registerPercentile.TotalMilliseconds} ms");
        Assert.True(loginPercentile < MaximumPercentile95, $"p95 do login: {loginPercentile.TotalMilliseconds} ms");
        Assert.True(probePercentile < MaximumPercentile95, $"p95 da rota protegida: {probePercentile.TotalMilliseconds} ms");
    }

    private static async Task<TimeSpan> MeasurePercentile95Async(Func<Task<HttpResponseMessage>> sendRequest)
    {
        for (var warmUp = 0; warmUp < WarmUpRequestCount; warmUp++)
        {
            (await sendRequest()).EnsureSuccessStatusCode();
        }

        var durations = new List<TimeSpan>(MeasuredRequestCount);
        for (var measurement = 0; measurement < MeasuredRequestCount; measurement++)
        {
            var stopwatch = Stopwatch.StartNew();
            (await sendRequest()).EnsureSuccessStatusCode();
            durations.Add(stopwatch.Elapsed);
        }

        return durations.Order().ElementAt((int)Math.Ceiling(MeasuredRequestCount * 0.95) - 1);
    }
}

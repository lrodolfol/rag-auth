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
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient, "medicao@exemplo.com");
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

        // SC-002: o cadastro é medido com o envio de e-mail de confirmação ativo (fila + dispatcher em segundo plano).
        await factory.EmailSender.WaitForEmailAsync("cliente1@exemplo.com", TimeSpan.FromSeconds(5));
        Assert.True(registerPercentile < MaximumPercentile95, $"p95 do cadastro: {registerPercentile.TotalMilliseconds} ms");
        Assert.True(loginPercentile < MaximumPercentile95, $"p95 do login: {loginPercentile.TotalMilliseconds} ms");
        Assert.True(probePercentile < MaximumPercentile95, $"p95 da rota protegida: {probePercentile.TotalMilliseconds} ms");
    }

    [Fact]
    public async Task PasswordRecoveryRoutesStayUnderFiveHundredMillisecondsAndDoNotLeakAccountExistence()
    {
        var confirmedEmails = await RegisterConfirmedAccountsAsync(RecoveryRequestCount);

        var confirmedDurations = await MeasureEachAsync(confirmedEmails, RequestRecoveryAsync);
        var unknownDurations = await MeasureEachAsync(
            Enumerable.Range(1, RecoveryRequestCount).Select(index => $"inexistente{index}@exemplo.com").ToList(), RequestRecoveryAsync);
        var recoveryPaths = await WaitForRecoveryPathsAsync(confirmedEmails);
        var verifyDurations = await MeasureEachAsync(recoveryPaths, path => _httpClient.GetAsync(path));
        var resetDurations = await MeasureEachAsync(recoveryPaths, path => _httpClient.PutAsJsonAsync(
            path, new { newPassword = "NovaSenha123", newPasswordConfirmation = "NovaSenha123" }));

        // SC-003: o tempo de resposta não pode revelar se o e-mail tem conta.
        var medianDifference = (Median(confirmedDurations) - Median(unknownDurations)).Duration();
        Assert.True(medianDifference < TimeSpan.FromMilliseconds(50), $"Diferença entre medianas: {medianDifference.TotalMilliseconds} ms");
        Assert.True(Percentile95(confirmedDurations) < MaximumPercentile95, $"p95 do pedido (conta confirmada): {Percentile95(confirmedDurations).TotalMilliseconds} ms");
        Assert.True(Percentile95(unknownDurations) < MaximumPercentile95, $"p95 do pedido (e-mail desconhecido): {Percentile95(unknownDurations).TotalMilliseconds} ms");
        Assert.True(Percentile95(verifyDurations) < MaximumPercentile95, $"p95 da verificação: {Percentile95(verifyDurations).TotalMilliseconds} ms");
        Assert.True(Percentile95(resetDurations) < MaximumPercentile95, $"p95 da redefinição: {Percentile95(resetDurations).TotalMilliseconds} ms");
    }

    private const int RecoveryRequestCount = 20;

    private async Task<List<string>> RegisterConfirmedAccountsAsync(int count)
    {
        var emails = new List<string>(count);
        for (var index = 1; index <= count; index++)
        {
            var account = await TestClientAccounts.RegisterPendingAsync(_httpClient, $"recuperacao{index}@exemplo.com");
            await TestClientAccounts.ConfirmEmailInDatabaseAsync(factory, account.ClientId);
            emails.Add(account.Email);
        }

        await factory.WaitForEmailQueueToDrainAsync();
        return emails;
    }

    // SC-001: pelo menos 19 de 20 e-mails de recuperação chegam ao remetente em menos de 60 s.
    private async Task<List<string>> WaitForRecoveryPathsAsync(IReadOnlyList<string> emails)
    {
        var paths = new List<string>(emails.Count);
        foreach (var email in emails)
        {
            var recoveryEmail = await factory.EmailSender.WaitForEmailAsync(email, FakeEmailSender.PasswordRecoverySubjectPrefix, TimeSpan.FromSeconds(60));
            paths.Add(FakeEmailSender.ExtractRecoveryPath(recoveryEmail));
        }

        Assert.True(paths.Count >= 19);
        return paths;
    }

    private Task<HttpResponseMessage> RequestRecoveryAsync(string email) =>
        _httpClient.PostAsJsonAsync("/api/v1/client/new-pass-recuvery-request", new { email });

    private static async Task<List<TimeSpan>> MeasureEachAsync<TInput>(IEnumerable<TInput> inputs, Func<TInput, Task<HttpResponseMessage>> sendRequest)
    {
        var durations = new List<TimeSpan>();
        foreach (var input in inputs)
        {
            var stopwatch = Stopwatch.StartNew();
            (await sendRequest(input)).EnsureSuccessStatusCode();
            durations.Add(stopwatch.Elapsed);
        }

        return durations;
    }

    private static TimeSpan Median(IReadOnlyCollection<TimeSpan> durations) => durations.Order().ElementAt(durations.Count / 2);

    private static TimeSpan Percentile95(IReadOnlyCollection<TimeSpan> durations) =>
        durations.Order().ElementAt((int)Math.Ceiling(durations.Count * 0.95) - 1);

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

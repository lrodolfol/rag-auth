using System.Net;
using Microsoft.Extensions.Logging;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RagFailureTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _httpClient = factory.CreateClient();

    public static TheoryData<string, int, int, string> RagStatusScenarios()
    {
        var scenarios = new TheoryData<string, int, int, string>();
        foreach (var method in new[] { "POST", "PUT", "DELETE" })
        {
            scenarios.Add(method, 400, 400, "O serviço de recursos recusou os dados enviados.");
            scenarios.Add(method, 404, 404, "O negócio não foi encontrado no serviço de recursos.");
            scenarios.Add(method, 409, 409, "O serviço de recursos indicou conflito com os dados enviados.");
            scenarios.Add(method, 422, 422, "O serviço de recursos recusou a operação.");
            scenarios.Add(method, 401, 502, "Falha na comunicação com o serviço de recursos. Tente novamente mais tarde.");
            scenarios.Add(method, 403, 502, "Falha na comunicação com o serviço de recursos. Tente novamente mais tarde.");
            scenarios.Add(method, 500, 502, "Falha na comunicação com o serviço de recursos. Tente novamente mais tarde.");
        }

        return scenarios;
    }

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [MemberData(nameof(RagStatusScenarios))]
    public async Task RagStatusIsTranslatedAndLocalDataIsKept(string method, int ragStatus, int expectedStatus, string expectedMessage)
    {
        var account = await PrepareAccountAsync(method);
        factory.RagApi.RespondWith((HttpStatusCode)ragStatus);

        var response = await SendAsync(method, account);

        await AssertFailureKeepsLocalDataAsync(response, method, (HttpStatusCode)expectedStatus, expectedMessage);
        if (ragStatus is 401 or 403)
        {
            Assert.Contains(factory.LogCapture.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("chave de acesso"));
        }
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ConnectionFailureReturnsServiceUnavailable(string method)
    {
        var account = await PrepareAccountAsync(method);
        factory.RagApi.FailWithConnectionError();

        var response = await SendAsync(method, account);

        await AssertFailureKeepsLocalDataAsync(response, method, HttpStatusCode.ServiceUnavailable, "O serviço de recursos está indisponível no momento.");
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task SlowRagReturnsGatewayTimeout(string method)
    {
        var account = await PrepareAccountAsync(method);
        factory.RagApi.DelayResponse(TimeSpan.FromSeconds(3));

        var response = await SendAsync(method, account);

        await AssertFailureKeepsLocalDataAsync(response, method, HttpStatusCode.GatewayTimeout, "O serviço de recursos demorou demais para responder.");
    }

    [Fact]
    public async Task LoginStillReturnsBusinessWhileRagIsDown()
    {
        var account = await PrepareAccountAsync("PUT");
        factory.RagApi.FailWithConnectionError();

        var login = await TestClientAccounts.LoginAsync(_httpClient, account.Email);

        Assert.Equal("Padaria da Ana", login.GetProperty("business").GetProperty("name").GetString());
    }

    private async Task<TestClientAccount> PrepareAccountAsync(string method)
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(_httpClient);
        if (method != "POST")
        {
            var createRequest = TestClientAccounts.CreateRequest(
                HttpMethod.Post,
                BusinessEndpointsTests.BusinessPath(account.ClientId),
                account.AccessToken,
                BusinessEndpointsTests.CreateValidBusiness());
            await ApiEnvelopeAssertions.AssertSuccessAsync(await _httpClient.SendAsync(createRequest), HttpStatusCode.Created);
        }

        return account;
    }

    private Task<HttpResponseMessage> SendAsync(string method, TestClientAccount account)
    {
        var business = BusinessEndpointsTests.CreateValidBusiness();
        business["name"] = "Nome alterado";
        var body = method == "DELETE" ? null : business;
        var request = TestClientAccounts.CreateRequest(new HttpMethod(method), BusinessEndpointsTests.BusinessPath(account.ClientId), account.AccessToken, body);
        return _httpClient.SendAsync(request);
    }

    private async Task AssertFailureKeepsLocalDataAsync(HttpResponseMessage response, string method, HttpStatusCode expectedStatus, string expectedMessage)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("stacktrace-secreto-da-rag", responseBody);
        await ApiEnvelopeAssertions.AssertErrorAsync(response, expectedStatus, expectedMessage);
        var business = (await TestClientAccounts.LoginAsync(_httpClient)).GetProperty("business");
        if (method == "POST")
        {
            Assert.Equal(System.Text.Json.JsonValueKind.Null, business.ValueKind);
        }
        else
        {
            Assert.Equal("Padaria da Ana", business.GetProperty("name").GetString());
        }
    }
}

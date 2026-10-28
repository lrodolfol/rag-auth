using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RagAuth.IntegrationTests.Infrastructure;

public sealed record TestClientAccount(int ClientId, string AccessCode, string Email, string Password, string AccessToken);

public static class TestClientAccounts
{
    public const string DefaultPassword = "senhaSegura1";

    public static async Task<TestClientAccount> RegisterAndLoginAsync(HttpClient httpClient, string email = "ana@exemplo.com")
    {
        var registerResponse = await httpClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name = "Ana Souza", email, password = DefaultPassword });
        var registeredClient = await ApiEnvelopeAssertions.AssertSuccessAsync(registerResponse, HttpStatusCode.Created);
        var login = await LoginAsync(httpClient, email);
        return new TestClientAccount(
            registeredClient.GetProperty("clientId").GetInt32(),
            registeredClient.GetProperty("accessCode").GetString()!,
            email,
            DefaultPassword,
            login.GetProperty("accessToken").GetString()!);
    }

    public static async Task<System.Text.Json.JsonElement> LoginAsync(HttpClient httpClient, string email = "ana@exemplo.com")
    {
        var loginResponse = await httpClient.PostAsJsonAsync("/api/v1/auth/login", new { email, password = DefaultPassword });
        return await ApiEnvelopeAssertions.AssertSuccessAsync(loginResponse, HttpStatusCode.OK);
    }

    public static HttpRequestMessage CreateRequest(HttpMethod method, string path, string? accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}

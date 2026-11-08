using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RagAuth.IntegrationTests.Infrastructure;

public sealed record TestClientAccount(int ClientId, string AccessCode, string Email, string Password, string AccessToken);

public sealed record PendingTestClientAccount(int ClientId, string AccessCode, string Email, string Password);

public static class TestClientAccounts
{
    public const string DefaultPassword = "senhaSegura1";
    public const string RegisterPath = "/api/v1/auth/register";

    /// <summary>Cadastra, marca o e-mail como confirmado direto no banco e faz login.</summary>
    public static async Task<TestClientAccount> RegisterAndLoginAsync(
        RagAuthApiFactory factory,
        HttpClient httpClient,
        string email = "ana@exemplo.com")
    {
        var pendingAccount = await RegisterPendingAsync(httpClient, email);
        await ConfirmEmailInDatabaseAsync(factory, pendingAccount.ClientId);
        var login = await LoginAsync(httpClient, email);
        return new TestClientAccount(
            pendingAccount.ClientId,
            pendingAccount.AccessCode,
            email,
            DefaultPassword,
            login.GetProperty("accessToken").GetString()!);
    }

    /// <summary>Só cadastra: a conta fica pendente de confirmação.</summary>
    public static async Task<PendingTestClientAccount> RegisterPendingAsync(
        HttpClient httpClient,
        string email = "ana@exemplo.com",
        string name = "Ana Souza")
    {
        var registerResponse = await httpClient.PostAsJsonAsync(RegisterPath, new { name, email, password = DefaultPassword });
        var registeredClient = await ApiEnvelopeAssertions.AssertSuccessAsync(registerResponse, HttpStatusCode.Created);
        return new PendingTestClientAccount(
            registeredClient.GetProperty("clientId").GetInt32(),
            registeredClient.GetProperty("accessCode").GetString()!,
            registeredClient.GetProperty("email").GetString()!,
            DefaultPassword);
    }

    public static Task ConfirmEmailInDatabaseAsync(RagAuthApiFactory factory, int clientId) =>
        factory.ExecuteInDatabaseAsync(databaseContext => databaseContext.Clients
            .Where(client => client.Id == clientId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(client => client.EmailConfirmed, true)
                .SetProperty(client => client.EmailConfirmedAt, DateTimeOffset.UtcNow)));

    public static async Task<JsonElement> LoginAsync(HttpClient httpClient, string email = "ana@exemplo.com")
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

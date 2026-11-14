using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class EmailConfirmationEndpointsTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    public const string NewLinkPath = "/api/v1/client/new-confirmation-register";
    private const string ConfirmedMessage = "E-mail confirmado com sucesso. Sua conta está ativa e você já pode fazer login.";
    private const string AlreadyConfirmedMessage = "Este e-mail já foi confirmado. Você já pode fazer login.";
    private const string InvalidLinkMessage = "Link de confirmação inválido. Use o link do e-mail de confirmação mais recente.";
    private const string ExpiredLinkMessage = "O link de confirmação expirou. Solicite um novo link de confirmação.";
    private const string NewLinkAcceptedMessage = "Se o e-mail estiver cadastrado e aguardando confirmação, enviaremos um novo link em instantes.";
    private static readonly TimeSpan EmailTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- US1: receber e usar o link ----------

    [Fact]
    public async Task RegisterSendsExactlyOneConfirmationEmailToRegisteredAddress()
    {
        await TestClientAccounts.RegisterPendingAsync(_httpClient, "Ana@Exemplo.com");

        var email = await factory.EmailSender.WaitForEmailAsync("ana@exemplo.com", EmailTimeout);
        await factory.WaitForEmailQueueToDrainAsync();

        Assert.Single(factory.EmailSender.SentEmails);
        Assert.Equal("Confirme seu cadastro no RagAuth", email.Subject);
        Assert.Contains("https://ragauth.com.br/", email.HtmlBody);
        Assert.Contains("24 horas", email.HtmlBody);
        Assert.Contains("solicite um novo link", email.HtmlBody);
        Assert.Matches("/api/v1/client/[A-Za-z0-9_-]{22}/confirmation-register/[A-Za-z0-9_-]{43}$", FakeEmailSender.ExtractConfirmationPath(email));
    }

    [Fact]
    public async Task RegisterResponseKeepsTheSameContract()
    {
        var response = await _httpClient.PostAsJsonAsync(TestClientAccounts.RegisterPath, new { name = "Ana", email = "ana@exemplo.com", password = TestClientAccounts.DefaultPassword });

        var data = await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Created, "Cadastro realizado com sucesso. Confirme o cadastro com seu e-mail");
        var propertyNames = data.EnumerateObject().Select(property => property.Name).Order().ToArray();
        Assert.Equal(["accessCode", "clientId", "createdAt", "email", "name"], propertyNames);
    }

    [Fact]
    public async Task LinkConfirmsEmailAndRepeatingReturnsAlreadyConfirmed()
    {
        var account = await TestClientAccounts.RegisterPendingAsync(_httpClient);
        var confirmationPath = await WaitForConfirmationPathAsync(account.Email);

        var firstResponse = await _httpClient.GetAsync(confirmationPath);
        var secondResponse = await _httpClient.GetAsync(confirmationPath);

        var firstData = await ApiEnvelopeAssertions.AssertSuccessAsync(firstResponse, HttpStatusCode.OK, ConfirmedMessage);
        Assert.Equal("confirmed", firstData.GetProperty("status").GetString());
        var secondData = await ApiEnvelopeAssertions.AssertSuccessAsync(secondResponse, HttpStatusCode.OK, AlreadyConfirmedMessage);
        Assert.Equal("alreadyConfirmed", secondData.GetProperty("status").GetString());
        var client = await LoadClientAsync(account.ClientId);
        Assert.True(client.EmailConfirmed);
        Assert.NotNull(client.EmailConfirmedAt);
    }

    [Fact]
    public async Task TrailingSlashIsAcceptedAndNoTokenIsRequired()
    {
        var account = await TestClientAccounts.RegisterPendingAsync(_httpClient);
        var confirmationPath = await WaitForConfirmationPathAsync(account.Email);

        var response = await _httpClient.GetAsync(confirmationPath + "/");

        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.OK, ConfirmedMessage);
    }

    [Fact]
    public async Task TamperedUnknownOrMalformedLinksReturnTheSameInvalidResponse()
    {
        var account = await TestClientAccounts.RegisterPendingAsync(_httpClient);
        var segments = (await WaitForConfirmationPathAsync(account.Email)).Split('/');
        var selector = segments[4];
        var validator = segments[6];
        var invalidPaths = new[]
        {
            ConfirmationPath(selector, ChangeLastCharacter(validator)),
            ConfirmationPath(ChangeLastCharacter(selector), validator),
            ConfirmationPath(selector[..21], validator),
            ConfirmationPath(selector, validator + "A"),
            ConfirmationPath("isto-nao-e-um-selector", "nem-um-validator"),
        };

        foreach (var invalidPath in invalidPaths)
        {
            var error = await ApiEnvelopeAssertions.AssertErrorAsync(await _httpClient.GetAsync(invalidPath), HttpStatusCode.BadRequest, InvalidLinkMessage);
            Assert.False(error.TryGetProperty("errors", out _));
        }

        Assert.False((await LoadClientAsync(account.ClientId)).EmailConfirmed);
    }

    [Fact]
    public async Task ExpiredLinkReturnsGoneAndAccountStaysPending()
    {
        var account = await TestClientAccounts.RegisterPendingAsync(_httpClient);
        var confirmationPath = await WaitForConfirmationPathAsync(account.Email);
        factory.Clock.Advance(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));

        var response = await _httpClient.GetAsync(confirmationPath);

        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.Gone, ExpiredLinkMessage);
        Assert.Equal("Link expirado", error.GetProperty("title").GetString());
        Assert.False((await LoadClientAsync(account.ClientId)).EmailConfirmed);
    }

    [Fact]
    public async Task EachClientGetsDistinctLinkThatDoesNotConfirmAnotherAccount()
    {
        var ana = await TestClientAccounts.RegisterPendingAsync(_httpClient, "ana@exemplo.com");
        var bia = await TestClientAccounts.RegisterPendingAsync(_httpClient, "bia@exemplo.com");
        var anaSegments = (await WaitForConfirmationPathAsync(ana.Email)).Split('/');
        var biaSegments = (await WaitForConfirmationPathAsync(bia.Email)).Split('/');

        var mixedResponse = await _httpClient.GetAsync(ConfirmationPath(anaSegments[4], biaSegments[6]));

        Assert.NotEqual(anaSegments[4], biaSegments[4]);
        Assert.NotEqual(anaSegments[6], biaSegments[6]);
        await ApiEnvelopeAssertions.AssertErrorAsync(mixedResponse, HttpStatusCode.BadRequest, InvalidLinkMessage);
        Assert.False((await LoadClientAsync(ana.ClientId)).EmailConfirmed);
        Assert.False((await LoadClientAsync(bia.ClientId)).EmailConfirmed);
    }

    [Fact]
    public async Task LinkOfRemovedAccountIsInvalid()
    {
        var account = await TestClientAccounts.RegisterPendingAsync(_httpClient);
        var confirmationPath = await WaitForConfirmationPathAsync(account.Email);
        await RemoveAccountAsync(account.ClientId);

        var response = await _httpClient.GetAsync(confirmationPath);

        await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest, InvalidLinkMessage);
    }

    [Fact]
    public async Task SenderFailingThreeTimesKeepsRegistrationAndAccountPending()
    {
        factory.EmailSender.FailNextAttempts(3);

        var account = await TestClientAccounts.RegisterPendingAsync(_httpClient);
        await factory.EmailSender.WaitForAttemptsAsync(3, EmailTimeout);
        await factory.WaitForEmailQueueToDrainAsync();

        Assert.Equal(3, factory.EmailSender.AttemptCount);
        Assert.Empty(factory.EmailSender.SentEmails);
        Assert.False((await LoadClientAsync(account.ClientId)).EmailConfirmed);
        Assert.Equal(3, factory.LogCapture.Entries.Count(entry => entry.Level == LogLevel.Warning && entry.Category == typeof(EmailDispatcher).FullName));
        Assert.Contains(factory.LogCapture.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("esgotou as 3 tentativas"));
    }

    [Fact]
    public async Task SenderFailingOnceDeliversOnRetry()
    {
        factory.EmailSender.FailNextAttempts(1);

        await TestClientAccounts.RegisterPendingAsync(_httpClient);

        await factory.EmailSender.WaitForEmailAsync("ana@exemplo.com", EmailTimeout);
        Assert.Equal(2, factory.EmailSender.AttemptCount);
    }

    // ---------- US3: novo link ----------

    [Fact]
    public async Task NewLinkRightAfterRegistrationIsAcceptedButSendsNothing()
    {
        await RegisterAndWaitForFirstEmailAsync();

        var response = await RequestNewLinkAsync("ana@exemplo.com");
        await factory.WaitForEmailQueueToDrainAsync();

        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Accepted, NewLinkAcceptedMessage);
        Assert.Single(factory.EmailSender.SentEmails);
    }

    [Fact]
    public async Task NewLinkAfterIntervalRevokesPreviousAndNewOneConfirms()
    {
        var firstEmail = await RegisterAndWaitForFirstEmailAsync();
        factory.Clock.Advance(TimeSpan.FromMinutes(5));

        var response = await RequestNewLinkAsync("ANA@exemplo.com");
        var secondEmail = await factory.EmailSender.WaitForEmailAsync("ana@exemplo.com", EmailTimeout, expectedCount: 2);

        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Accepted, NewLinkAcceptedMessage);
        var oldLinkResponse = await _httpClient.GetAsync(FakeEmailSender.ExtractConfirmationPath(firstEmail));
        await ApiEnvelopeAssertions.AssertErrorAsync(oldLinkResponse, HttpStatusCode.BadRequest, InvalidLinkMessage);
        var newLinkResponse = await _httpClient.GetAsync(FakeEmailSender.ExtractConfirmationPath(secondEmail));
        await ApiEnvelopeAssertions.AssertSuccessAsync(newLinkResponse, HttpStatusCode.OK, ConfirmedMessage);
    }

    [Fact]
    public async Task ExpiredLinkCanBeReplacedByNewLink()
    {
        await RegisterAndWaitForFirstEmailAsync();
        factory.Clock.Advance(TimeSpan.FromHours(25));

        await RequestNewLinkAsync("ana@exemplo.com");
        var newEmail = await factory.EmailSender.WaitForEmailAsync("ana@exemplo.com", EmailTimeout, expectedCount: 2);

        var response = await _httpClient.GetAsync(FakeEmailSender.ExtractConfirmationPath(newEmail));
        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.OK, ConfirmedMessage);
    }

    [Fact]
    public async Task EleventhNewLinkWithinTwentyFourHoursSendsNoEmail()
    {
        var account = await TestClientAccounts.RegisterPendingAsync(_httpClient);
        for (var request = 1; request <= 10; request++)
        {
            factory.Clock.Advance(TimeSpan.FromMinutes(5));
            await RequestNewLinkAsync(account.Email);
            await factory.EmailSender.WaitForEmailAsync(account.Email, EmailTimeout, expectedCount: request + 1);
        }

        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var response = await RequestNewLinkAsync(account.Email);
        await factory.WaitForEmailQueueToDrainAsync();

        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Accepted, NewLinkAcceptedMessage);
        Assert.Equal(11, factory.EmailSender.EmailsTo(account.Email).Count);
        Assert.Equal(11, await CountLinksAsync(account.ClientId));
    }

    [Fact]
    public async Task ResponsesForUnknownConfirmedAndRemovedEmailsAreIdenticalToPendingOne()
    {
        await RegisterAndWaitForFirstEmailAsync("pendente@exemplo.com");
        var confirmed = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient, "confirmada@exemplo.com");
        var removed = await TestClientAccounts.RegisterPendingAsync(_httpClient, "removida@exemplo.com");
        await RemoveAccountAsync(removed.ClientId);
        await factory.WaitForEmailQueueToDrainAsync();
        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var emailCountBefore = factory.EmailSender.SentEmails.Count;

        var pendingBody = await ReadNewLinkBodyAsync("pendente@exemplo.com");
        var otherBodies = new[]
        {
            await ReadNewLinkBodyAsync("inexistente@exemplo.com"),
            await ReadNewLinkBodyAsync(confirmed.Email),
            await ReadNewLinkBodyAsync("removida@exemplo.com"),
        };
        await factory.WaitForEmailQueueToDrainAsync();

        Assert.All(otherBodies, body => Assert.Equal(pendingBody, body));
        Assert.Equal(emailCountBefore + 1, factory.EmailSender.SentEmails.Count);
        Assert.Equal(2, factory.EmailSender.EmailsTo("pendente@exemplo.com").Count);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"email\": \"\" }")]
    [InlineData("{ \"email\": \"nao-e-email\" }")]
    [InlineData("LONG")]
    public async Task InvalidNewLinkBodyReturnsValidationProblemForEmail(string body)
    {
        var resolvedBody = body == "LONG" ? $"{{ \"email\": \"{new string('a', 250)}@exemplo.com\" }}" : body;

        var response = await _httpClient.PostAsync(NewLinkPath, new StringContent(resolvedBody, Encoding.UTF8, "application/json"));

        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest, "Um ou mais campos são inválidos.");
        ApiEnvelopeAssertions.AssertFieldError(error, "email");
    }

    [Fact]
    public async Task NewLinkRouteAcceptsTrailingSlash()
    {
        var response = await _httpClient.PostAsJsonAsync(NewLinkPath + "/", new { email = "inexistente@exemplo.com" });

        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Accepted, NewLinkAcceptedMessage);
    }

    [Fact]
    public async Task SimultaneousRequestsIssueAtMostOneNewLink()
    {
        var account = await TestClientAccounts.RegisterPendingAsync(_httpClient);
        factory.Clock.Advance(TimeSpan.FromMinutes(5));

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => RequestNewLinkAsync(account.Email)));
        await factory.WaitForEmailQueueToDrainAsync();

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        Assert.Equal(2, await CountLinksAsync(account.ClientId));
        Assert.Equal(1, await CountActiveLinksAsync(account.ClientId));
        Assert.Equal(2, factory.EmailSender.EmailsTo(account.Email).Count);
    }

    private async Task<ComposedEmail> RegisterAndWaitForFirstEmailAsync(string email = "ana@exemplo.com")
    {
        await TestClientAccounts.RegisterPendingAsync(_httpClient, email);
        return await factory.EmailSender.WaitForEmailAsync(email, EmailTimeout);
    }

    private async Task<string> WaitForConfirmationPathAsync(string email) =>
        FakeEmailSender.ExtractConfirmationPath(await factory.EmailSender.WaitForEmailAsync(email, EmailTimeout));

    private Task<HttpResponseMessage> RequestNewLinkAsync(string email) =>
        _httpClient.PostAsJsonAsync(NewLinkPath, new { email });

    private async Task<string> ReadNewLinkBodyAsync(string email)
    {
        var response = await RequestNewLinkAsync(email);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string ConfirmationPath(string selector, string validator) =>
        $"/api/v1/client/{selector}/confirmation-register/{validator}";

    private static string ChangeLastCharacter(string value) =>
        string.Concat(value.AsSpan(0, value.Length - 1), value[^1] == 'A' ? "B" : "A");

    private async Task<Client> LoadClientAsync(int clientId)
    {
        Client? client = null;
        await factory.ExecuteInDatabaseAsync(async databaseContext => client = await databaseContext.Clients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(storedClient => storedClient.Id == clientId));
        return client!;
    }

    private Task RemoveAccountAsync(int clientId) =>
        factory.ExecuteInDatabaseAsync(databaseContext => databaseContext.Clients
            .Where(client => client.Id == clientId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(client => client.DeletedAt, DateTimeOffset.UtcNow)));

    private async Task<int> CountLinksAsync(int clientId)
    {
        var count = 0;
        await factory.ExecuteInDatabaseAsync(async databaseContext =>
            count = await databaseContext.EmailConfirmationLinks.CountAsync(link => link.ClientId == clientId));
        return count;
    }

    private async Task<int> CountActiveLinksAsync(int clientId)
    {
        var count = 0;
        await factory.ExecuteInDatabaseAsync(async databaseContext => count = await databaseContext.EmailConfirmationLinks
            .CountAsync(link => link.ClientId == clientId && link.UsedAt == null && link.RevokedAt == null));
        return count;
    }
}

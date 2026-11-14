using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class PasswordRecoveryEndpointsTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    public const string RequestPath = "/api/v1/client/new-pass-recuvery-request";
    private const string AcceptedMessage = "Se o e-mail estiver cadastrado, enviaremos um link para redefinir a senha em instantes.";
    private const string ValidLinkMessage = "Link válido. Informe a nova senha.";
    private const string PasswordResetMessage = "Senha alterada com sucesso. Você já pode fazer login com a nova senha.";
    private const string InvalidLinkMessage = "Link de redefinição de senha inválido. Use o link do e-mail de recuperação mais recente.";
    private const string ExpiredLinkMessage = "O link de redefinição de senha expirou. Solicite uma nova recuperação de senha.";
    private const string ValidationMessage = "Um ou mais campos são inválidos.";
    private const string NewPassword = "NovaSenha123";
    private static readonly TimeSpan EmailTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- US1: pedir a recuperação ----------

    [Fact]
    public async Task ConfirmedAccountReceivesRecoveryEmailWithLink()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);

        var response = await RequestRecoveryAsync("ana@exemplo.com");

        var data = await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Accepted, AcceptedMessage);
        Assert.Equal(JsonValueKind.Null, data.ValueKind);
        var email = await WaitForRecoveryEmailAsync("ana@exemplo.com");
        Assert.Equal("Redefina sua senha no RagAuth", email.Subject);
        Assert.Matches("/api/v1/client/[A-Za-z0-9_-]{22}/pass-recuvery/[A-Za-z0-9_-]{43}$", FakeEmailSender.ExtractRecoveryPath(email));
        Assert.Contains("https://ragauth.com.br/", email.HtmlBody);
        Assert.Contains("24 horas", email.HtmlBody);
        Assert.Contains("ignore este e-mail", email.HtmlBody);
    }

    [Fact]
    public async Task UnknownPendingAndRemovedEmailsGetIdenticalResponseAndNoEmail()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient, "confirmada@exemplo.com");
        await TestClientAccounts.RegisterPendingAsync(_httpClient, "pendente@exemplo.com");
        var removed = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient, "removida@exemplo.com");
        await RemoveAccountAsync(removed.ClientId);
        await factory.WaitForEmailQueueToDrainAsync();

        var confirmedBody = await ReadRequestBodyAsync("confirmada@exemplo.com");
        var otherBodies = new[]
        {
            await ReadRequestBodyAsync("inexistente@exemplo.com"),
            await ReadRequestBodyAsync("pendente@exemplo.com"),
            await ReadRequestBodyAsync("removida@exemplo.com"),
        };
        await factory.WaitForEmailQueueToDrainAsync();

        Assert.All(otherBodies, body => Assert.Equal(confirmedBody, body));
        Assert.Single(factory.EmailSender.EmailsTo("confirmada@exemplo.com", FakeEmailSender.PasswordRecoverySubjectPrefix));
        Assert.Empty(factory.EmailSender.EmailsTo("pendente@exemplo.com", FakeEmailSender.PasswordRecoverySubjectPrefix));
        Assert.Empty(factory.EmailSender.EmailsTo("removida@exemplo.com", FakeEmailSender.PasswordRecoverySubjectPrefix));
        Assert.Empty(factory.EmailSender.EmailsTo("inexistente@exemplo.com"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"email\": \"invalido\" }")]
    [InlineData("LONG")]
    public async Task InvalidRequestBodyReturnsValidationProblemForEmail(string body)
    {
        var resolvedBody = body == "LONG" ? $"{{ \"email\": \"{new string('a', 243)}@exemplo.com\" }}" : body;

        var response = await _httpClient.PostAsync(RequestPath, new StringContent(resolvedBody, Encoding.UTF8, "application/json"));

        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest, ValidationMessage);
        ApiEnvelopeAssertions.AssertFieldError(error, "email");
    }

    [Fact]
    public async Task TwoClientsReceiveDifferentLinks()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient, "ana@exemplo.com");
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient, "bia@exemplo.com");

        var anaPath = await RequestAndExtractRecoveryPathAsync("ana@exemplo.com");
        var biaPath = await RequestAndExtractRecoveryPathAsync("bia@exemplo.com");

        Assert.NotEqual(anaPath.Split('/')[4], biaPath.Split('/')[4]);
        Assert.NotEqual(anaPath.Split('/')[6], biaPath.Split('/')[6]);
    }

    [Fact]
    public async Task RequestRouteWorksWithoutTokenAndWithTrailingSlash()
    {
        var response = await _httpClient.PostAsJsonAsync(RequestPath + "/", new { email = "inexistente@exemplo.com" });

        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Accepted, AcceptedMessage);
    }

    // ---------- US2: verificar o link e redefinir a senha ----------

    [Fact]
    public async Task VerifyReturnsValidStatusWithoutAccountDataAndDoesNotConsumeLink()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var recoveryPath = await RequestAndExtractRecoveryPathAsync("ana@exemplo.com");

        var first = await ApiEnvelopeAssertions.AssertSuccessAsync(await _httpClient.GetAsync(recoveryPath), HttpStatusCode.OK, ValidLinkMessage);
        var second = await _httpClient.GetAsync(recoveryPath + "/");

        Assert.Equal("valid", first.GetProperty("status").GetString());
        Assert.Single(first.EnumerateObject());
        await ApiEnvelopeAssertions.AssertSuccessAsync(second, HttpStatusCode.OK, ValidLinkMessage);
    }

    [Fact]
    public async Task MismatchedConfirmationReturnsFieldErrorAndKeepsLinkValid()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var recoveryPath = await RequestAndExtractRecoveryPathAsync("ana@exemplo.com");

        var response = await ResetAsync(recoveryPath, NewPassword, "OutraSenha123");

        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest, ValidationMessage);
        ApiEnvelopeAssertions.AssertFieldError(error, "newPasswordConfirmation");
        Assert.Contains("A confirmação da senha deve ser igual à nova senha.", error.GetProperty("errors").GetProperty("newPasswordConfirmation")[0].GetString());
        Assert.Equal(HttpStatusCode.OK, (await _httpClient.GetAsync(recoveryPath)).StatusCode);
    }

    [Theory]
    [InlineData("curta1")]
    [InlineData("somenteletras")]
    [InlineData("LONG")]
    public async Task PasswordOutsideRulesReturnsFieldErrorAndKeepsLinkValid(string password)
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var recoveryPath = await RequestAndExtractRecoveryPathAsync("ana@exemplo.com");
        var resolvedPassword = password == "LONG" ? new string('a', 128) + "1" : password;

        var response = await ResetAsync(recoveryPath, resolvedPassword, resolvedPassword);

        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest, ValidationMessage);
        ApiEnvelopeAssertions.AssertFieldError(error, "newPassword");
        Assert.Equal(HttpStatusCode.OK, (await _httpClient.GetAsync(recoveryPath)).StatusCode);
    }

    [Fact]
    public async Task ValidResetChangesPasswordKeepsOldTokensAndSendsNoticeWithoutLink()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var recoveryPath = await RequestAndExtractRecoveryPathAsync(account.Email);

        var response = await ResetAsync(recoveryPath, NewPassword, NewPassword);

        var data = await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.OK, PasswordResetMessage);
        Assert.Equal(JsonValueKind.Null, data.ValueKind);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(account.Email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(account.Email, TestClientAccounts.DefaultPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendProbeAsync(account.ClientId, account.AccessToken)).StatusCode);
        var notice = await factory.EmailSender.WaitForEmailAsync(account.Email, FakeEmailSender.PasswordChangedSubjectPrefix, EmailTimeout);
        Assert.DoesNotContain("pass-recuvery", notice.HtmlBody);
        Assert.DoesNotContain(NewPassword, notice.HtmlBody);
        Assert.NotNull((await LoadClientAsync(account.ClientId)).PasswordChangedAt);
    }

    [Fact]
    public async Task SecondResetWithSameLinkIsInvalid()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var recoveryPath = await RequestAndExtractRecoveryPathAsync("ana@exemplo.com");
        await ApiEnvelopeAssertions.AssertSuccessAsync(await ResetAsync(recoveryPath, NewPassword, NewPassword), HttpStatusCode.OK);

        var response = await ResetAsync(recoveryPath, "OutraSenha456", "OutraSenha456");

        await AssertInvalidLinkAsync(response);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ana@exemplo.com", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task TamperedValidatorIsInvalidOnVerifyAndReset()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var segments = (await RequestAndExtractRecoveryPathAsync("ana@exemplo.com")).Split('/');
        var tamperedPath = RecoveryPath(segments[4], ChangeLastCharacter(segments[6]));

        await AssertInvalidLinkAsync(await _httpClient.GetAsync(tamperedPath));
        await AssertInvalidLinkAsync(await ResetAsync(tamperedPath, NewPassword, NewPassword));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ana@exemplo.com", TestClientAccounts.DefaultPassword)).StatusCode);
    }

    [Fact]
    public async Task EmailConfirmationLinkIsNotAcceptedOnRecoveryRoute()
    {
        var pending = await TestClientAccounts.RegisterPendingAsync(_httpClient);
        var confirmationEmail = await factory.EmailSender.WaitForEmailAsync(pending.Email, FakeEmailSender.ConfirmationSubjectPrefix, EmailTimeout);
        var segments = FakeEmailSender.ExtractConfirmationPath(confirmationEmail).Split('/');
        await TestClientAccounts.ConfirmEmailInDatabaseAsync(factory, pending.ClientId);

        await AssertInvalidLinkAsync(await _httpClient.GetAsync(RecoveryPath(segments[4], segments[6])));
        await AssertInvalidLinkAsync(await ResetAsync(RecoveryPath(segments[4], segments[6]), NewPassword, NewPassword));
    }

    [Fact]
    public async Task LinkOfAccountRemovedAfterIssueIsInvalid()
    {
        var account = await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var recoveryPath = await RequestAndExtractRecoveryPathAsync(account.Email);
        await RemoveAccountAsync(account.ClientId);

        await AssertInvalidLinkAsync(await _httpClient.GetAsync(recoveryPath));
        await AssertInvalidLinkAsync(await ResetAsync(recoveryPath, NewPassword, NewPassword));
    }

    [Fact]
    public async Task SimultaneousResetsChangePasswordOnlyOnce()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var recoveryPath = await RequestAndExtractRecoveryPathAsync("ana@exemplo.com");
        var passwords = new[] { "PrimeiraSenha1", "SegundaSenha2" };

        var responses = await Task.WhenAll(passwords.Select(password => ResetAsync(recoveryPath, password, password)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        var winner = passwords[Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.OK)];
        var loser = passwords.Single(password => password != winner);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ana@exemplo.com", winner)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("ana@exemplo.com", loser)).StatusCode);
    }

    [Fact]
    public async Task ErrorInstanceDoesNotExposeSelectorOrValidator()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var segments = (await RequestAndExtractRecoveryPathAsync("ana@exemplo.com")).Split('/');
        var tamperedValidator = ChangeLastCharacter(segments[6]);

        var getError = await ApiEnvelopeAssertions.AssertErrorAsync(
            await _httpClient.GetAsync(RecoveryPath(segments[4], tamperedValidator)), HttpStatusCode.BadRequest);
        var putError = await ApiEnvelopeAssertions.AssertErrorAsync(
            await ResetAsync(RecoveryPath(segments[4], tamperedValidator), NewPassword, NewPassword), HttpStatusCode.BadRequest);

        foreach (var instance in new[] { getError.GetProperty("instance").GetString()!, putError.GetProperty("instance").GetString()! })
        {
            Assert.DoesNotContain(segments[4], instance);
            Assert.DoesNotContain(tamperedValidator, instance);
        }
    }

    // ---------- US3: link expirado e novo pedido ----------

    [Fact]
    public async Task LinkIsValidUntilExpirationAndGoneOneSecondLater()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var recoveryPath = await RequestAndExtractRecoveryPathAsync("ana@exemplo.com");
        var expiresAt = await LoadSingleLinkExpirationAsync();
        factory.Clock.SetUtcNow(expiresAt - TimeSpan.FromMilliseconds(500));
        Assert.Equal(HttpStatusCode.OK, (await _httpClient.GetAsync(recoveryPath)).StatusCode);

        factory.Clock.SetUtcNow(expiresAt + TimeSpan.FromSeconds(1));

        var getError = await ApiEnvelopeAssertions.AssertErrorAsync(await _httpClient.GetAsync(recoveryPath), HttpStatusCode.Gone, ExpiredLinkMessage);
        await ApiEnvelopeAssertions.AssertErrorAsync(await ResetAsync(recoveryPath, NewPassword, NewPassword), HttpStatusCode.Gone, ExpiredLinkMessage);
        Assert.Equal("Link expirado", getError.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ana@exemplo.com", TestClientAccounts.DefaultPassword)).StatusCode);
    }

    [Fact]
    public async Task NewRequestAfterIntervalRevokesPreviousLink()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        var firstPath = await RequestAndExtractRecoveryPathAsync("ana@exemplo.com");
        factory.Clock.Advance(TimeSpan.FromMinutes(5));

        var secondPath = await RequestAndExtractRecoveryPathAsync("ana@exemplo.com", expectedCount: 2);

        await AssertInvalidLinkAsync(await _httpClient.GetAsync(firstPath));
        Assert.Equal(HttpStatusCode.OK, (await _httpClient.GetAsync(secondPath)).StatusCode);
    }

    [Fact]
    public async Task RequestWithinFiveMinutesGetsSameResponseButNoNewEmail()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        await RequestAndExtractRecoveryPathAsync("ana@exemplo.com");
        factory.Clock.Advance(TimeSpan.FromMinutes(4));

        var response = await RequestRecoveryAsync("ana@exemplo.com");
        await factory.WaitForEmailQueueToDrainAsync();

        await ApiEnvelopeAssertions.AssertSuccessAsync(response, HttpStatusCode.Accepted, AcceptedMessage);
        Assert.Single(factory.EmailSender.EmailsTo("ana@exemplo.com", FakeEmailSender.PasswordRecoverySubjectPrefix));
    }

    [Fact]
    public async Task EleventhRequestWithinTwentyFourHoursSendsNoEmailUntilWindowMoves()
    {
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient);
        for (var request = 1; request <= 10; request++)
        {
            await RequestRecoveryAsync("ana@exemplo.com");
            await WaitForRecoveryEmailAsync("ana@exemplo.com", request);
            factory.Clock.Advance(TimeSpan.FromMinutes(5));
        }

        await ApiEnvelopeAssertions.AssertSuccessAsync(await RequestRecoveryAsync("ana@exemplo.com"), HttpStatusCode.Accepted, AcceptedMessage);
        await factory.WaitForEmailQueueToDrainAsync();
        Assert.Equal(10, factory.EmailSender.EmailsTo("ana@exemplo.com", FakeEmailSender.PasswordRecoverySubjectPrefix).Count);

        factory.Clock.Advance(TimeSpan.FromHours(24));
        await RequestRecoveryAsync("ana@exemplo.com");
        await WaitForRecoveryEmailAsync("ana@exemplo.com", 11);
    }

    [Fact]
    public async Task RecoveryLimitIsIndependentFromConfirmationLimit()
    {
        await TestClientAccounts.RegisterPendingAsync(_httpClient, "pendente@exemplo.com");
        await TestClientAccounts.RegisterAndLoginAsync(factory, _httpClient, "confirmada@exemplo.com");
        await factory.WaitForEmailQueueToDrainAsync();

        await _httpClient.PostAsJsonAsync(EmailConfirmationEndpointsTests.NewLinkPath, new { email = "pendente@exemplo.com" });
        await RequestRecoveryAsync("confirmada@exemplo.com");

        await WaitForRecoveryEmailAsync("confirmada@exemplo.com");
        Assert.Empty(factory.EmailSender.EmailsTo("pendente@exemplo.com", FakeEmailSender.PasswordRecoverySubjectPrefix));
    }

    private Task<HttpResponseMessage> RequestRecoveryAsync(string email) =>
        _httpClient.PostAsJsonAsync(RequestPath, new { email });

    private async Task<string> ReadRequestBodyAsync(string email)
    {
        var response = await RequestRecoveryAsync(email);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private Task<ComposedEmail> WaitForRecoveryEmailAsync(string email, int expectedCount = 1) =>
        factory.EmailSender.WaitForEmailAsync(email, FakeEmailSender.PasswordRecoverySubjectPrefix, EmailTimeout, expectedCount);

    private async Task<string> RequestAndExtractRecoveryPathAsync(string email, int expectedCount = 1)
    {
        await ApiEnvelopeAssertions.AssertSuccessAsync(await RequestRecoveryAsync(email), HttpStatusCode.Accepted);
        return FakeEmailSender.ExtractRecoveryPath(await WaitForRecoveryEmailAsync(email, expectedCount));
    }

    private Task<HttpResponseMessage> ResetAsync(string recoveryPath, string newPassword, string newPasswordConfirmation) =>
        _httpClient.PutAsJsonAsync(recoveryPath, new { newPassword, newPasswordConfirmation });

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _httpClient.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    private Task<HttpResponseMessage> SendProbeAsync(int clientId, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, string.Format(ProtectedProbeStartupFilter.ProbePathTemplate, clientId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _httpClient.SendAsync(request);
    }

    private static async Task AssertInvalidLinkAsync(HttpResponseMessage response)
    {
        var error = await ApiEnvelopeAssertions.AssertErrorAsync(response, HttpStatusCode.BadRequest, InvalidLinkMessage);
        Assert.False(error.TryGetProperty("errors", out _));
    }

    private static string RecoveryPath(string selector, string validator) =>
        $"/api/v1/client/{selector}/pass-recuvery/{validator}";

    private static string ChangeLastCharacter(string value) =>
        string.Concat(value.AsSpan(0, value.Length - 1), value[^1] == 'A' ? "B" : "A");

    private Task RemoveAccountAsync(int clientId) =>
        factory.ExecuteInDatabaseAsync(databaseContext => databaseContext.Clients
            .Where(client => client.Id == clientId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(client => client.DeletedAt, DateTimeOffset.UtcNow)));

    private async Task<Client> LoadClientAsync(int clientId)
    {
        Client? client = null;
        await factory.ExecuteInDatabaseAsync(async databaseContext => client = await databaseContext.Clients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(storedClient => storedClient.Id == clientId));
        return client!;
    }

    private async Task<DateTimeOffset> LoadSingleLinkExpirationAsync()
    {
        var expiresAt = default(DateTimeOffset);
        await factory.ExecuteInDatabaseAsync(async databaseContext =>
            expiresAt = await databaseContext.PasswordRecoveryLinks.Select(link => link.ExpiresAt).SingleAsync());
        return expiresAt;
    }
}

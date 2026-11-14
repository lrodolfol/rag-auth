using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using RagAuth.Api.Infrastructure.Email;

namespace RagAuth.IntegrationTests.Infrastructure;

/// <summary>Substitui o Resend nos testes: guarda os e-mails "enviados" e simula falhas.</summary>
public sealed partial class FakeEmailSender : IEmailSender
{
    public const string PasswordRecoverySubjectPrefix = "Redefina sua senha no";
    public const string PasswordChangedSubjectPrefix = "Sua senha foi alterada no";
    public const string ConfirmationSubjectPrefix = "Confirme seu cadastro no";

    private readonly ConcurrentQueue<ComposedEmail> _sentEmails = new();
    private int _remainingFailures;
    private int _attemptCount;

    public IReadOnlyList<ComposedEmail> SentEmails => _sentEmails.ToArray();

    public int AttemptCount => Volatile.Read(ref _attemptCount);

    public void FailNextAttempts(int failureCount) => Interlocked.Exchange(ref _remainingFailures, failureCount);

    public void Reset()
    {
        _sentEmails.Clear();
        Interlocked.Exchange(ref _remainingFailures, 0);
        Interlocked.Exchange(ref _attemptCount, 0);
    }

    public Task SendAsync(ComposedEmail email, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _attemptCount);
        if (Interlocked.Decrement(ref _remainingFailures) >= 0)
        {
            throw new HttpRequestException("Falha simulada no provedor de e-mail.");
        }

        Interlocked.Exchange(ref _remainingFailures, 0);
        _sentEmails.Enqueue(email);
        return Task.CompletedTask;
    }

    public IReadOnlyList<ComposedEmail> EmailsTo(string recipient) =>
        SentEmails.Where(email => email.RecipientEmail == recipient).ToList();

    /// <summary>Filtra o tipo de e-mail pelo começo do assunto.</summary>
    public IReadOnlyList<ComposedEmail> EmailsTo(string recipient, string subjectStartsWith) =>
        SentEmails
            .Where(email => email.RecipientEmail == recipient && email.Subject.StartsWith(subjectStartsWith, StringComparison.Ordinal))
            .ToList();

    public Task<ComposedEmail> WaitForEmailAsync(string recipient, TimeSpan timeout, int expectedCount = 1) =>
        WaitForAsync(() => EmailsTo(recipient), recipient, timeout, expectedCount);

    public Task<ComposedEmail> WaitForEmailAsync(string recipient, string subjectStartsWith, TimeSpan timeout, int expectedCount = 1) =>
        WaitForAsync(() => EmailsTo(recipient, subjectStartsWith), recipient, timeout, expectedCount);

    public async Task WaitForAttemptsAsync(int expectedAttempts, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (AttemptCount < expectedAttempts && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(AttemptCount >= expectedAttempts, $"Esperadas {expectedAttempts} tentativas, ocorreram {AttemptCount}.");
    }

    /// <summary>Converte o link do e-mail (página do front-end) na rota da API.</summary>
    public static string ExtractConfirmationPath(ComposedEmail email)
    {
        var match = ConfirmationLinkPattern().Match(email.HtmlBody);
        Assert.True(match.Success, "O e-mail não contém o link de confirmação.");
        return $"/api/v1/client/{match.Groups["selector"].Value}/confirmation-register/{match.Groups["validator"].Value}";
    }

    /// <summary>Converte <c>…/{selector}/pass-recuvery/{validator}</c> na rota da API.</summary>
    public static string ExtractRecoveryPath(ComposedEmail email)
    {
        var match = RecoveryLinkPattern().Match(email.HtmlBody);
        Assert.True(match.Success, "O e-mail não contém o link de recuperação de senha.");
        return $"/api/v1/client/{match.Groups["selector"].Value}/pass-recuvery/{match.Groups["validator"].Value}";
    }

    private static async Task<ComposedEmail> WaitForAsync(
        Func<IReadOnlyList<ComposedEmail>> findEmails, string recipient, TimeSpan timeout, int expectedCount)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var emails = findEmails();
            if (emails.Count >= expectedCount)
            {
                return emails[expectedCount - 1];
            }

            await Task.Delay(20);
        }

        throw new Xunit.Sdk.XunitException($"Nenhum e-mail nº {expectedCount} para {recipient} em {timeout}.");
    }

    [GeneratedRegex("href=\"https?://[^\"]+/(?<selector>[A-Za-z0-9_-]{22})/confirmation-register/(?<validator>[A-Za-z0-9_-]{43})\"")]
    private static partial Regex ConfirmationLinkPattern();

    [GeneratedRegex("href=\"https?://[^\"]+/(?<selector>[A-Za-z0-9_-]{22})/pass-recuvery/(?<validator>[A-Za-z0-9_-]{43})\"")]
    private static partial Regex RecoveryLinkPattern();
}

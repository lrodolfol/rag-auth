using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using RagAuth.Api.Infrastructure.Email;

namespace RagAuth.IntegrationTests.Infrastructure;

/// <summary>Substitui o Resend nos testes: guarda os e-mails "enviados" e simula falhas.</summary>
public sealed partial class FakeConfirmationEmailSender : IConfirmationEmailSender
{
    private readonly ConcurrentQueue<ConfirmationEmail> _sentEmails = new();
    private int _remainingFailures;
    private int _attemptCount;

    public IReadOnlyList<ConfirmationEmail> SentEmails => _sentEmails.ToArray();

    public int AttemptCount => Volatile.Read(ref _attemptCount);

    public void FailNextAttempts(int failureCount) => Interlocked.Exchange(ref _remainingFailures, failureCount);

    public void Reset()
    {
        _sentEmails.Clear();
        Interlocked.Exchange(ref _remainingFailures, 0);
        Interlocked.Exchange(ref _attemptCount, 0);
    }

    public Task SendAsync(ConfirmationEmail email, CancellationToken cancellationToken)
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

    public IReadOnlyList<ConfirmationEmail> EmailsTo(string recipient) =>
        SentEmails.Where(email => email.RecipientEmail == recipient).ToList();

    public async Task<ConfirmationEmail> WaitForEmailAsync(string recipient, TimeSpan timeout, int expectedCount = 1)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var emails = EmailsTo(recipient);
            if (emails.Count >= expectedCount)
            {
                return emails[expectedCount - 1];
            }

            await Task.Delay(20);
        }

        throw new Xunit.Sdk.XunitException($"Nenhum e-mail nº {expectedCount} para {recipient} em {timeout}.");
    }

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
    public static string ExtractConfirmationPath(ConfirmationEmail email)
    {
        var match = ConfirmationLinkPattern().Match(email.HtmlBody);
        Assert.True(match.Success, "O e-mail não contém o link de confirmação.");
        return $"/api/v1/client/{match.Groups["selector"].Value}/confirmation-register/{match.Groups["validator"].Value}";
    }

    [GeneratedRegex("href=\"https?://[^\"]+/(?<selector>[A-Za-z0-9_-]{22})/confirmation-register/(?<validator>[A-Za-z0-9_-]{43})\"")]
    private static partial Regex ConfirmationLinkPattern();
}

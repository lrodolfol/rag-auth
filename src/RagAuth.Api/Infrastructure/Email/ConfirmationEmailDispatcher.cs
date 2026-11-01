using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Observability;
using RagAuth.Api.Services.EmailConfirmation;

namespace RagAuth.Api.Infrastructure.Email;

/// <summary>
/// Consome a fila e envia os e-mails com até <see cref="MaximumAttempts"/> tentativas (research R6).
/// A espera entre tentativas roda fora do laço de leitura e nunca bloqueia as demais mensagens.
/// Nenhuma exceção de envio escapa deste serviço.
/// </summary>
public sealed class ConfirmationEmailDispatcher(
    IConfirmationEmailQueue emailQueue,
    IConfirmationEmailComposer emailComposer,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider,
    ILogger<ConfirmationEmailDispatcher> logger) : BackgroundService
{
    public const int MaximumAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in emailQueue.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(message, stoppingToken);
        }
    }

    internal async Task ProcessAsync(ConfirmationEmailMessage message, CancellationToken stoppingToken)
    {
        try
        {
            await SendAsync(message, stoppingToken);
            LogMessages.ConfirmationEmailSent(logger, message.ClientId, message.AttemptNumber);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("fail");
        }
        catch (Exception exception)
        {
            HandleFailedAttempt(message, exception, stoppingToken);
        }
    }

    // O IResend depende de IOptionsSnapshot (escopo): um escopo por envio.
    private async Task SendAsync(ConfirmationEmailMessage message, CancellationToken cancellationToken)
    {
        var email = emailComposer.Compose(message);
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IConfirmationEmailSender>();
        await emailSender.SendAsync(email, cancellationToken);
    }

    private void HandleFailedAttempt(ConfirmationEmailMessage message, Exception exception, CancellationToken stoppingToken)
    {
        LogMessages.ConfirmationEmailAttemptFailed(logger, message.ClientId, message.AttemptNumber, exception.GetType().Name);
        if (message.AttemptNumber >= MaximumAttempts)
        {
            LogMessages.ConfirmationEmailDeliveryExhausted(logger, message.ClientId, MaximumAttempts);
            return;
        }

        var nextAttempt = message with { AttemptNumber = message.AttemptNumber + 1 };
        _ = RetryAfterDelayAsync(nextAttempt, RetryDelayAfter(message.AttemptNumber), stoppingToken);
    }

    private async Task RetryAfterDelayAsync(ConfirmationEmailMessage message, TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, timeProvider, stoppingToken);
            if (!emailQueue.TryEnqueue(message))
            {
                LogMessages.ConfirmationEmailQueueRejected(logger, message.ClientId);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private TimeSpan RetryDelayAfter(int failedAttemptNumber)
    {
        var retryDelaysSeconds = emailOptions.Value.RetryDelaysSeconds;
        if (retryDelaysSeconds.Length == 0)
        {
            return TimeSpan.Zero;
        }

        var delayIndex = Math.Min(failedAttemptNumber - 1, retryDelaysSeconds.Length - 1);
        return TimeSpan.FromSeconds(retryDelaysSeconds[delayIndex]);
    }
}

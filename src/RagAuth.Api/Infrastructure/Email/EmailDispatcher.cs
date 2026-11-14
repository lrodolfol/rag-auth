using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Observability;

namespace RagAuth.Api.Infrastructure.Email;

/// <summary>
/// Consome a fila e envia os e-mails com até <see cref="MaximumAttempts"/> tentativas (research R11).
/// A espera entre tentativas roda fora do laço de leitura e nunca bloqueia as demais mensagens.
/// Nenhuma exceção de envio escapa deste serviço.
/// </summary>
public sealed class EmailDispatcher(
    IEmailQueue emailQueue,
    IEmailComposer emailComposer,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider,
    ILogger<EmailDispatcher> logger) : BackgroundService
{
    public const int MaximumAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in emailQueue.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(message, stoppingToken);
        }
    }

    internal async Task ProcessAsync(OutgoingEmailMessage message, CancellationToken stoppingToken)
    {
        try
        {
            await SendAsync(message, stoppingToken);
            LogMessages.EmailSent(logger, message.Kind, message.ClientId, message.AttemptNumber);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HandleFailedAttempt(message, exception, stoppingToken);
        }
    }

    // O IResend depende de IOptionsSnapshot (escopo): um escopo por envio.
    private async Task SendAsync(OutgoingEmailMessage message, CancellationToken cancellationToken)
    {
        var email = emailComposer.Compose(message);
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        await emailSender.SendAsync(email, cancellationToken);
    }

    private void HandleFailedAttempt(OutgoingEmailMessage message, Exception exception, CancellationToken stoppingToken)
    {
        LogMessages.EmailAttemptFailed(logger, message.Kind, message.ClientId, message.AttemptNumber, exception.GetType().Name);
        if (message.AttemptNumber >= MaximumAttempts)
        {
            LogMessages.EmailDeliveryExhausted(logger, message.Kind, message.ClientId, MaximumAttempts);
            return;
        }

        var nextAttempt = message with { AttemptNumber = message.AttemptNumber + 1 };
        _ = RetryAfterDelayAsync(nextAttempt, RetryDelayAfter(message.AttemptNumber), stoppingToken);
    }

    private async Task RetryAfterDelayAsync(OutgoingEmailMessage message, TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, timeProvider, stoppingToken);
            if (!emailQueue.TryEnqueue(message))
            {
                LogMessages.EmailQueueRejected(logger, message.Kind, message.ClientId);
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

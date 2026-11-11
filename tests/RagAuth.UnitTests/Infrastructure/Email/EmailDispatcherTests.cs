using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RagAuth.Api.Configuration;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Infrastructure.Email;

public sealed class ConfirmationEmailDispatcherTests
{
    private const int SentEventId = 1500;
    private const int AttemptFailedEventId = 1501;
    private const int ExhaustedEventId = 1502;

    private readonly ChannelConfirmationEmailQueue _queue = new();
    private readonly RecordingEmailSender _sender = new();
    private readonly AdjustableTimeProvider _timeProvider = new();
    private readonly Mock<ILogger<ConfirmationEmailDispatcher>> _logger = LoggerMocks.Create<ConfirmationEmailDispatcher>();
    private readonly Mock<IConfirmationEmailComposer> _composer = new();

    public ConfirmationEmailDispatcherTests() =>
        _composer.Setup(composer => composer.Compose(It.IsAny<ConfirmationEmailMessage>()))
            .Returns<ConfirmationEmailMessage>(message => new ConfirmationEmail(message.RecipientEmail, "Assunto", "Corpo"));

    [Fact]
    public async Task SuccessfulFirstAttemptLogsSent()
    {
        await CreateDispatcher().ProcessAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal(1, _sender.AttemptCount);
        Assert.Equal([SentEventId], _logger.LoggedEventIds());
        Assert.Equal(0, _timeProvider.PendingTimerCount);
    }

    [Fact]
    public async Task FailureIsRetriedAfterConfiguredDelayAndThenSucceeds()
    {
        _sender.FailuresBeforeSuccess = 1;
        var dispatcher = CreateDispatcher();

        await dispatcher.ProcessAsync(CreateMessage(), CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(0, _queue.PendingCount);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        var retriedMessage = await DequeueAsync();
        await dispatcher.ProcessAsync(retriedMessage, CancellationToken.None);

        Assert.Equal(2, retriedMessage.AttemptNumber);
        Assert.Equal(2, _sender.AttemptCount);
        Assert.Equal([AttemptFailedEventId, SentEventId], _logger.LoggedEventIds());
    }

    [Fact]
    public async Task ThreeFailuresLogExhaustionAndStopRetrying()
    {
        _sender.FailuresBeforeSuccess = int.MaxValue;
        var dispatcher = CreateDispatcher();

        await dispatcher.ProcessAsync(CreateMessage(), CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        await dispatcher.ProcessAsync(await DequeueAsync(), CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromSeconds(119));
        Assert.Equal(0, _queue.PendingCount);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        await dispatcher.ProcessAsync(await DequeueAsync(), CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromHours(1));

        Assert.Equal(3, _sender.AttemptCount);
        Assert.Equal(0, _queue.PendingCount);
        Assert.Equal(0, _timeProvider.PendingTimerCount);
        Assert.Equal([AttemptFailedEventId, AttemptFailedEventId, AttemptFailedEventId, ExhaustedEventId], _logger.LoggedEventIds());
    }

    [Fact]
    public async Task ComposerOrSenderExceptionNeverEscapes()
    {
        _composer.Setup(composer => composer.Compose(It.IsAny<ConfirmationEmailMessage>())).Throws(new InvalidOperationException("falha"));

        var exception = await Record.ExceptionAsync(() => CreateDispatcher().ProcessAsync(CreateMessage(attemptNumber: 3), CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal([AttemptFailedEventId, ExhaustedEventId], _logger.LoggedEventIds());
    }

    [Fact]
    public async Task MessageWaitingForRetryDoesNotBlockTheNextOne()
    {
        _sender.FailuresBeforeSuccess = 1;
        var dispatcher = CreateDispatcher();

        await dispatcher.ProcessAsync(CreateMessage("ana@exemplo.com"), CancellationToken.None);
        await dispatcher.ProcessAsync(CreateMessage("bia@exemplo.com"), CancellationToken.None);

        Assert.Equal(1, _timeProvider.PendingTimerCount);
        Assert.Equal(["bia@exemplo.com"], _sender.SentRecipients);
    }

    [Fact]
    public async Task BackgroundLoopConsumesQueueUntilStopped()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.StartAsync(CancellationToken.None);

        _queue.TryEnqueue(CreateMessage());
        await WaitUntilAsync(() => _sender.AttemptCount == 1);
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal(["ana@exemplo.com"], _sender.SentRecipients);
    }

    [Fact]
    public async Task EmptyRetryConfigurationRetriesImmediately()
    {
        _sender.FailuresBeforeSuccess = 1;

        await CreateDispatcher(retryDelaysSeconds: []).ProcessAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal(2, (await DequeueAsync()).AttemptNumber);
    }

    private ConfirmationEmailDispatcher CreateDispatcher(int[]? retryDelaysSeconds = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IConfirmationEmailSender>(_ => _sender);
        var serviceProvider = services.BuildServiceProvider();
        var emailOptions = new EmailOptions { RetryDelaysSeconds = retryDelaysSeconds ?? [30, 120] };
        return new ConfirmationEmailDispatcher(
            _queue,
            _composer.Object,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(emailOptions),
            _timeProvider,
            _logger.Object);
    }

    private async Task<ConfirmationEmailMessage> DequeueAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var message in _queue.ReadAllAsync(timeout.Token))
        {
            return message;
        }

        throw new InvalidOperationException("A fila foi encerrada.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static ConfirmationEmailMessage CreateMessage(string recipient = "ana@exemplo.com", int attemptNumber = 1) =>
        new(42, recipient, "Ana", "https://ragauth.com.br/link", attemptNumber);

    private sealed class RecordingEmailSender : IConfirmationEmailSender
    {
        private readonly List<string> _sentRecipients = [];

        public int FailuresBeforeSuccess { get; set; }

        public int AttemptCount { get; private set; }

        public IReadOnlyList<string> SentRecipients => _sentRecipients;

        public Task SendAsync(ConfirmationEmail email, CancellationToken cancellationToken)
        {
            AttemptCount++;
            if (FailuresBeforeSuccess > 0)
            {
                FailuresBeforeSuccess--;
                throw new HttpRequestException("Falha simulada.");
            }

            _sentRecipients.Add(email.RecipientEmail);
            return Task.CompletedTask;
        }
    }
}

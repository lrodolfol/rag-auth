using Moq;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.EmailConfirmation;

public sealed class EmailConfirmationServiceRequestNewLinkTests
{
    private const int NewLinkIssuedEventId = 1507;
    private const int NewLinkThrottledEventId = 1508;
    private const int QueueRejectedEventId = 1509;

    private readonly EmailConfirmationServiceFixture _fixture = new();
    private readonly Client _client;
    private readonly EmailConfirmationLink _activeLink;
    private readonly IssuedConfirmationLink _newLink;

    public EmailConfirmationServiceRequestNewLinkTests()
    {
        _client = _fixture.CreatePendingClient();
        _activeLink = new EmailConfirmationLink { ClientId = _client.Id, Client = _client };
        var message = new ConfirmationEmailMessage(_client.Id, _client.Email, _client.Name, "https://ragauth.com.br/novo");
        _newLink = new IssuedConfirmationLink(new EmailConfirmationLink { Client = _client }, "validator", message);
        _fixture.ClientRepository.Setup(repository => repository.FindPendingByEmailAsync("ana@exemplo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_client);
        _fixture.LinkRepository.Setup(repository => repository.FindActiveForClientAsync(_client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activeLink);
        _fixture.LinkIssuer.Setup(issuer => issuer.Issue(_client, ConfirmationLinkOrigin.NewLinkRequest)).Returns(_newLink);
    }

    [Fact]
    public async Task PendingAccountWithinLimitsGetsNewLinkAndPreviousIsRevoked()
    {
        await _fixture.CreateService().RequestNewLinkAsync("  ANA@Exemplo.com ");

        Assert.Equal(_fixture.TimeProvider.GetUtcNow(), _activeLink.RevokedAt);
        _fixture.LinkIssuer.Verify(issuer => issuer.Issue(_client, ConfirmationLinkOrigin.NewLinkRequest), Times.Once());
        _fixture.UnitOfWorkMocks.VerifyCommitted();
        _fixture.ClientRepository.Verify(repository => repository.LockForUpdateAsync(_client.Id, It.IsAny<CancellationToken>()), Times.Once());
        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(_newLink.Message), Times.Once());
        Assert.Equal([NewLinkIssuedEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task PendingAccountWithoutActiveLinkStillGetsNewLink()
    {
        _fixture.LinkRepository.Setup(repository => repository.FindActiveForClientAsync(_client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailConfirmationLink?)null);

        await _fixture.CreateService().RequestNewLinkAsync("ana@exemplo.com");

        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(_newLink.Message), Times.Once());
    }

    [Fact]
    public async Task BlockedByPolicyDoesNothingAndLogsThrottle()
    {
        _fixture.IssuePolicy.Setup(policy => policy.CanIssueAsync(_client.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _fixture.CreateService().RequestNewLinkAsync("ana@exemplo.com");

        AssertNothingIssued();
        Assert.Null(_activeLink.RevokedAt);
        _fixture.UnitOfWorkMocks.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
        Assert.Equal([NewLinkThrottledEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task UnknownRemovedOrConfirmedEmailDoesNothing()
    {
        await _fixture.CreateService().RequestNewLinkAsync("outra@exemplo.com");

        AssertNothingIssued();
        Assert.Empty(_fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task ConcurrentIssueViolationIsTreatedAsLimitWithoutException()
    {
        _fixture.UnitOfWorkMocks.UnitOfWork.SetupSequence(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .ThrowsAsync(UniqueViolationExceptions.For(UniqueIndexNames.ActiveEmailConfirmationLinkPerClient));

        var exception = await Record.ExceptionAsync(() => _fixture.CreateService().RequestNewLinkAsync("ana@exemplo.com"));

        Assert.Null(exception);
        _fixture.UnitOfWorkMocks.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(It.IsAny<ConfirmationEmailMessage>()), Times.Never());
        Assert.Equal([NewLinkThrottledEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task OtherDatabaseErrorsPropagate()
    {
        _fixture.UnitOfWorkMocks.UnitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolationExceptions.For("outro_indice"));

        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => _fixture.CreateService().RequestNewLinkAsync("ana@exemplo.com"));
    }

    [Fact]
    public async Task FullQueueIsLoggedWithoutFailingTheRequest()
    {
        _fixture.EmailQueue.Setup(queue => queue.TryEnqueue(It.IsAny<ConfirmationEmailMessage>())).Returns(false);

        await _fixture.CreateService().RequestNewLinkAsync("ana@exemplo.com");

        Assert.Equal([NewLinkIssuedEventId, QueueRejectedEventId], _fixture.Logger.LoggedEventIds());
    }

    private void AssertNothingIssued()
    {
        _fixture.LinkIssuer.Verify(issuer => issuer.Issue(It.IsAny<Client>(), It.IsAny<string>()), Times.Never());
        _fixture.UnitOfWorkMocks.UnitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(It.IsAny<ConfirmationEmailMessage>()), Times.Never());
    }
}

using Microsoft.EntityFrameworkCore;
using Moq;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Services.PasswordRecovery;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.PasswordRecovery;

public sealed class PasswordRecoveryServiceRequestTests
{
    private const int LinkIssuedEventId = 1600;
    private const int ThrottledEventId = 1601;
    private const int IneligibleEmailEventId = 1602;
    private const int QueueRejectedEventId = 1509;

    private readonly PasswordRecoveryServiceFixture _fixture = new();
    private readonly Client _client;
    private readonly PasswordRecoveryLink _activeLink;
    private readonly IssuedPasswordRecoveryLink _newLink;

    public PasswordRecoveryServiceRequestTests()
    {
        _client = _fixture.CreateConfirmedClient();
        _activeLink = new PasswordRecoveryLink { ClientId = _client.Id, Client = _client };
        var message = new OutgoingEmailMessage(EmailKind.PasswordRecovery, _client.Id, _client.Email, _client.Name, "https://ragauth.com.br/novo");
        _newLink = new IssuedPasswordRecoveryLink(new PasswordRecoveryLink { Client = _client }, message);
        _fixture.ClientRepository.Setup(repository => repository.FindConfirmedByEmailAsync("ana@exemplo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_client);
        _fixture.LinkRepository.Setup(repository => repository.FindActiveForClientAsync(_client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activeLink);
        _fixture.LinkIssuer.Setup(issuer => issuer.Issue(_client)).Returns(_newLink);
    }

    [Fact]
    public async Task ConfirmedAccountGetsNewLinkAndPreviousIsRevoked()
    {
        await _fixture.CreateService().RequestRecoveryAsync("  ANA@Exemplo.com ");

        Assert.Equal(_fixture.TimeProvider.GetUtcNow(), _activeLink.RevokedAt);
        _fixture.ClientRepository.Verify(repository => repository.LockForUpdateAsync(_client.Id, It.IsAny<CancellationToken>()), Times.Once());
        _fixture.LinkIssuer.Verify(issuer => issuer.Issue(_client), Times.Once());
        _fixture.UnitOfWorkMocks.VerifyCommitted();
        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(_newLink.Message), Times.Once());
        Assert.Equal([LinkIssuedEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task ConfirmedAccountWithoutActiveLinkStillGetsNewLink()
    {
        _fixture.LinkRepository.Setup(repository => repository.FindActiveForClientAsync(_client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordRecoveryLink?)null);

        await _fixture.CreateService().RequestRecoveryAsync("ana@exemplo.com");

        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(_newLink.Message), Times.Once());
    }

    [Fact]
    public async Task IneligibleEmailDoesNothingAndLogsWithoutTheEmail()
    {
        var exception = await Record.ExceptionAsync(() => _fixture.CreateService().RequestRecoveryAsync("pendente@exemplo.com"));

        Assert.Null(exception);
        AssertNothingIssued();
        Assert.Equal([IneligibleEmailEventId], _fixture.Logger.LoggedEventIds());
        Assert.All(_fixture.Logger.LoggedMessages(), message => Assert.DoesNotContain("pendente", message));
    }

    [Fact]
    public async Task BlockedByPolicyDoesNothingAndLogsThrottle()
    {
        _fixture.IssuePolicy.Setup(policy => policy.CanIssueAsync(_client.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var exception = await Record.ExceptionAsync(() => _fixture.CreateService().RequestRecoveryAsync("ana@exemplo.com"));

        Assert.Null(exception);
        AssertNothingIssued();
        Assert.Null(_activeLink.RevokedAt);
        _fixture.UnitOfWorkMocks.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
        Assert.Equal([ThrottledEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task ConcurrentIssueViolationIsTreatedAsLimitWithoutException()
    {
        _fixture.UnitOfWorkMocks.UnitOfWork.SetupSequence(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .ThrowsAsync(UniqueViolationExceptions.For(UniqueIndexNames.ActivePasswordRecoveryLinkPerClient));

        var exception = await Record.ExceptionAsync(() => _fixture.CreateService().RequestRecoveryAsync("ana@exemplo.com"));

        Assert.Null(exception);
        _fixture.UnitOfWorkMocks.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>()), Times.Never());
        Assert.Equal([ThrottledEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task OtherDatabaseErrorsPropagate()
    {
        _fixture.UnitOfWorkMocks.UnitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolationExceptions.For("outro_indice"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _fixture.CreateService().RequestRecoveryAsync("ana@exemplo.com"));
    }

    [Fact]
    public async Task FullQueueIsLoggedWithRecoveryKindWithoutFailingTheRequest()
    {
        _fixture.EmailQueue.Setup(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>())).Returns(false);

        await _fixture.CreateService().RequestRecoveryAsync("ana@exemplo.com");

        Assert.Equal([LinkIssuedEventId, QueueRejectedEventId], _fixture.Logger.LoggedEventIds());
        Assert.Contains(nameof(EmailKind.PasswordRecovery), _fixture.Logger.LoggedMessages()[1]);
    }

    [Theory]
    [InlineData("ana@exemplo.com")]
    [InlineData("desconhecido@exemplo.com")]
    public async Task WholeRequestRunsInsideTheMinimumDurationGuard(string email)
    {
        var insideGuard = false;
        _fixture.DurationGuard.Setup(guard => guard.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (operation, cancellationToken) =>
            {
                insideGuard = true;
                await operation(cancellationToken);
                insideGuard = false;
            });
        _fixture.ClientRepository.Setup(repository => repository.FindConfirmedByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(insideGuard))
            .ReturnsAsync(email == "ana@exemplo.com" ? _client : null);

        await _fixture.CreateService().RequestRecoveryAsync(email);

        _fixture.DurationGuard.Verify(guard => guard.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once());
        Assert.False(insideGuard);
    }

    private void AssertNothingIssued()
    {
        _fixture.LinkIssuer.Verify(issuer => issuer.Issue(It.IsAny<Client>()), Times.Never());
        _fixture.UnitOfWorkMocks.UnitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>()), Times.Never());
    }
}

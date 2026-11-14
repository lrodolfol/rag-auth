using Moq;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.PasswordRecovery;

public sealed class PasswordRecoveryServiceResetTests
{
    private const string NewPassword = "NovaSenha123";
    private const int QueueRejectedEventId = 1509;
    private const int PasswordResetEventId = 1603;

    private readonly PasswordRecoveryServiceFixture _fixture = new();
    private readonly Client _client;

    public PasswordRecoveryServiceResetTests() => _client = _fixture.CreateConfirmedClient();

    [Fact]
    public async Task ValidLinkChangesPasswordConsumesLinkAndEnqueuesNotice()
    {
        var link = _fixture.SetupStoredLink(_client);
        var now = _fixture.TimeProvider.GetUtcNow();

        await _fixture.CreateService().ResetPasswordAsync(_fixture.Selector, _fixture.Validator, NewPassword);

        Assert.Equal(PasswordRecoveryServiceFixture.NewPasswordHash, _client.PasswordHash);
        Assert.Equal(now, _client.PasswordChangedAt);
        Assert.Equal(now, link.UsedAt);
        _fixture.PasswordHashing.Verify(hashing => hashing.HashPassword(_client, NewPassword), Times.Once());
        _fixture.ClientRepository.Verify(repository => repository.LockForUpdateAsync(_client.Id, It.IsAny<CancellationToken>()), Times.Once());
        _fixture.LinkRepository.Verify(repository => repository.ReloadAsync(link, It.IsAny<CancellationToken>()), Times.Once());
        _fixture.UnitOfWorkMocks.VerifyCommitted();
        Assert.Equal([PasswordResetEventId], _fixture.Logger.LoggedEventIds());
        var expectedNotice = new OutgoingEmailMessage(EmailKind.PasswordChanged, _client.Id, _client.Email, _client.Name, null, now);
        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(expectedNotice), Times.Once());
    }

    [Fact]
    public async Task AccessCodeIsNotChanged()
    {
        _fixture.SetupStoredLink(_client);

        await _fixture.CreateService().ResetPasswordAsync(_fixture.Selector, _fixture.Validator, NewPassword);

        Assert.Equal(PasswordRecoveryServiceFixture.AccessCode, _client.AccessCode);
    }

    [Fact]
    public async Task LinkConsumedByConcurrentRequestAfterLockIsInvalid()
    {
        var link = _fixture.SetupStoredLink(_client);
        _fixture.LinkRepository.Setup(repository => repository.ReloadAsync(link, It.IsAny<CancellationToken>()))
            .Callback(() => link.UsedAt = _fixture.TimeProvider.GetUtcNow())
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidPasswordRecoveryLinkException>(
            () => _fixture.CreateService().ResetPasswordAsync(_fixture.Selector, _fixture.Validator, NewPassword));

        AssertPasswordUnchanged();
        _fixture.UnitOfWorkMocks.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task LinkRevokedByConcurrentRequestAfterLockIsInvalid()
    {
        var link = _fixture.SetupStoredLink(_client);
        _fixture.LinkRepository.Setup(repository => repository.ReloadAsync(link, It.IsAny<CancellationToken>()))
            .Callback(() => link.RevokedAt = _fixture.TimeProvider.GetUtcNow())
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidPasswordRecoveryLinkException>(
            () => _fixture.CreateService().ResetPasswordAsync(_fixture.Selector, _fixture.Validator, NewPassword));

        AssertPasswordUnchanged();
    }

    [Fact]
    public async Task ExpiredLinkDoesNotChangePassword()
    {
        _fixture.SetupStoredLink(_client, link => link.ExpiresAt = _fixture.TimeProvider.GetUtcNow() - TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ExpiredPasswordRecoveryLinkException>(
            () => _fixture.CreateService().ResetPasswordAsync(_fixture.Selector, _fixture.Validator, NewPassword));

        AssertPasswordUnchanged();
    }

    [Theory]
    [InlineData("used")]
    [InlineData("revoked")]
    [InlineData("wrong-validator")]
    public async Task InvalidLinkDoesNotChangePassword(string state)
    {
        _fixture.SetupStoredLink(_client, link =>
        {
            link.UsedAt = state == "used" ? _fixture.TimeProvider.GetUtcNow() : null;
            link.RevokedAt = state == "revoked" ? _fixture.TimeProvider.GetUtcNow() : null;
        });
        var validator = state == "wrong-validator" ? _fixture.Generator.GenerateValidator() : _fixture.Validator;

        await Assert.ThrowsAsync<InvalidPasswordRecoveryLinkException>(
            () => _fixture.CreateService().ResetPasswordAsync(_fixture.Selector, validator, NewPassword));

        AssertPasswordUnchanged();
    }

    [Fact]
    public async Task FullQueueIsLoggedAndResetStillSucceeds()
    {
        _fixture.SetupStoredLink(_client);
        _fixture.EmailQueue.Setup(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>())).Returns(false);

        var exception = await Record.ExceptionAsync(
            () => _fixture.CreateService().ResetPasswordAsync(_fixture.Selector, _fixture.Validator, NewPassword));

        Assert.Null(exception);
        Assert.Equal(PasswordRecoveryServiceFixture.NewPasswordHash, _client.PasswordHash);
        Assert.Equal([PasswordResetEventId, QueueRejectedEventId], _fixture.Logger.LoggedEventIds());
        Assert.Contains(nameof(EmailKind.PasswordChanged), _fixture.Logger.LoggedMessages()[1]);
    }

    private void AssertPasswordUnchanged()
    {
        Assert.Equal("hash-da-senha-antiga", _client.PasswordHash);
        Assert.Null(_client.PasswordChangedAt);
        _fixture.PasswordHashing.Verify(hashing => hashing.HashPassword(It.IsAny<Client>(), It.IsAny<string>()), Times.Never());
        _fixture.EmailQueue.Verify(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>()), Times.Never());
    }
}

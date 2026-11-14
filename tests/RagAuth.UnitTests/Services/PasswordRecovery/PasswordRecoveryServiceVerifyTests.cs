using Moq;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.PasswordRecovery;

public sealed class PasswordRecoveryServiceVerifyTests
{
    private const int ExpiredEventId = 1604;
    private const int InvalidEventId = 1605;

    private readonly PasswordRecoveryServiceFixture _fixture = new();
    private readonly Client _client;

    public PasswordRecoveryServiceVerifyTests() => _client = _fixture.CreateConfirmedClient();

    [Fact]
    public async Task ActiveLinkIsAcceptedWithoutChangingAnything()
    {
        var link = _fixture.SetupStoredLink(_client);

        await _fixture.CreateService().VerifyLinkAsync(_fixture.Selector, _fixture.Validator);

        Assert.Null(link.UsedAt);
        Assert.Null(link.RevokedAt);
        _fixture.UnitOfWorkMocks.UnitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
        Assert.Empty(_fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task LinkIsStillValidExactlyAtExpiration()
    {
        _fixture.SetupStoredLink(_client, link => link.ExpiresAt = _fixture.TimeProvider.GetUtcNow());

        await _fixture.CreateService().VerifyLinkAsync(_fixture.Selector, _fixture.Validator);
    }

    [Fact]
    public async Task LinkAfterExpirationIsExpired()
    {
        _fixture.SetupStoredLink(_client, link => link.ExpiresAt = _fixture.TimeProvider.GetUtcNow() - TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ExpiredPasswordRecoveryLinkException>(
            () => _fixture.CreateService().VerifyLinkAsync(_fixture.Selector, _fixture.Validator));

        Assert.Equal([ExpiredEventId], _fixture.Logger.LoggedEventIds());
    }

    [Theory]
    [InlineData("used")]
    [InlineData("revoked")]
    [InlineData("unconfirmed")]
    public async Task UnusableLinkIsInvalid(string state)
    {
        _fixture.SetupStoredLink(_client, link =>
        {
            link.UsedAt = state == "used" ? _fixture.TimeProvider.GetUtcNow() : null;
            link.RevokedAt = state == "revoked" ? _fixture.TimeProvider.GetUtcNow() : null;
            link.Client.EmailConfirmed = state != "unconfirmed";
        });

        await AssertInvalidAsync(_fixture.Selector, _fixture.Validator);
    }

    [Fact]
    public async Task MalformedSelectorOrValidatorIsInvalid()
    {
        _fixture.SetupStoredLink(_client);

        await AssertInvalidAsync(_fixture.Selector[..21], _fixture.Validator);
        await AssertInvalidAsync(_fixture.Selector, _fixture.Validator[..42] + "+");
        _fixture.LinkRepository.Verify(
            repository => repository.FindBySelectorWithClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task UnknownSelectorIsInvalid() =>
        await AssertInvalidAsync(_fixture.Generator.GenerateSelector(), _fixture.Validator);

    [Fact]
    public async Task WrongValidatorIsInvalid()
    {
        _fixture.SetupStoredLink(_client);

        await AssertInvalidAsync(_fixture.Selector, _fixture.Generator.GenerateValidator());
    }

    [Fact]
    public async Task EmailConfirmationHashIsNotAcceptedAsRecoveryLink()
    {
        _fixture.SetupStoredLink(_client, purpose: EmailConfirmationLink.EmailConfirmationPurpose);

        await AssertInvalidAsync(_fixture.Selector, _fixture.Validator);
    }

    [Fact]
    public async Task HashIsCheckedBeforeRevealingExpiration()
    {
        _fixture.SetupStoredLink(_client, link => link.ExpiresAt = _fixture.TimeProvider.GetUtcNow() - TimeSpan.FromDays(1));

        await AssertInvalidAsync(_fixture.Selector, _fixture.Generator.GenerateValidator());
    }

    private async Task AssertInvalidAsync(string selector, string validator)
    {
        var eventsBefore = _fixture.Logger.LoggedEventIds().Count;

        await Assert.ThrowsAsync<InvalidPasswordRecoveryLinkException>(() => _fixture.CreateService().VerifyLinkAsync(selector, validator));

        Assert.Equal([InvalidEventId], _fixture.Logger.LoggedEventIds().Skip(eventsBefore));
        Assert.All(_fixture.Logger.LoggedMessages(), message =>
        {
            Assert.DoesNotContain(selector, message);
            Assert.DoesNotContain(validator, message);
        });
    }
}

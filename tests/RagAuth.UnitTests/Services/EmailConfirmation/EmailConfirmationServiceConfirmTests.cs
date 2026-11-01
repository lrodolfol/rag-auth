using Moq;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.EmailConfirmation;

public sealed class EmailConfirmationServiceConfirmTests
{
    private const int EmailConfirmedEventId = 1503;
    private const int ExpiredLinkEventId = 1504;
    private const int InvalidLinkEventId = 1505;

    private readonly EmailConfirmationServiceFixture _fixture = new();

    [Fact]
    public async Task ActiveLinkConfirmsEmailAndMarksLinkAsUsed()
    {
        var client = _fixture.CreatePendingClient();
        var link = _fixture.SetupStoredLink(client);

        var outcome = await ConfirmAsync();

        var now = _fixture.TimeProvider.GetUtcNow();
        Assert.Equal(EmailConfirmationOutcome.Confirmed, outcome);
        Assert.Equal(now, link.UsedAt);
        Assert.True(client.EmailConfirmed);
        Assert.Equal(now, client.EmailConfirmedAt);
        VerifySaved(Times.Once());
        Assert.Equal([EmailConfirmedEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task LinkIsStillValidAtExactExpirationInstant()
    {
        var client = _fixture.CreatePendingClient();
        _fixture.SetupStoredLink(client, link => link.ExpiresAt = _fixture.TimeProvider.GetUtcNow());

        Assert.Equal(EmailConfirmationOutcome.Confirmed, await ConfirmAsync());
    }

    [Fact]
    public async Task LinkOneTickAfterExpirationIsExpiredAndAccountStaysPending()
    {
        var client = _fixture.CreatePendingClient();
        _fixture.SetupStoredLink(client, link => link.ExpiresAt = _fixture.TimeProvider.GetUtcNow() - TimeSpan.FromTicks(1));

        await Assert.ThrowsAsync<ExpiredConfirmationLinkException>(ConfirmAsync);

        Assert.False(client.EmailConfirmed);
        VerifySaved(Times.Never());
        Assert.Equal([ExpiredLinkEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task UsedLinkReturnsAlreadyConfirmedWithoutChanges()
    {
        var client = _fixture.CreatePendingClient();
        client.EmailConfirmed = true;
        var usedAt = _fixture.TimeProvider.GetUtcNow() - TimeSpan.FromMinutes(10);
        _fixture.SetupStoredLink(client, link => link.UsedAt = usedAt);

        var outcome = await ConfirmAsync();

        Assert.Equal(EmailConfirmationOutcome.AlreadyConfirmed, outcome);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task RevokedLinkOfConfirmedAccountReturnsAlreadyConfirmed()
    {
        var client = _fixture.CreatePendingClient();
        client.EmailConfirmed = true;
        _fixture.SetupStoredLink(client, link => link.RevokedAt = _fixture.TimeProvider.GetUtcNow());

        Assert.Equal(EmailConfirmationOutcome.AlreadyConfirmed, await ConfirmAsync());
    }

    [Fact]
    public async Task RevokedLinkOfPendingAccountIsInvalid()
    {
        var client = _fixture.CreatePendingClient();
        _fixture.SetupStoredLink(client, link => link.RevokedAt = _fixture.TimeProvider.GetUtcNow());

        await Assert.ThrowsAsync<InvalidConfirmationLinkException>(ConfirmAsync);

        Assert.False(client.EmailConfirmed);
        Assert.Equal([InvalidLinkEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task UnknownSelectorIsInvalid()
    {
        await Assert.ThrowsAsync<InvalidConfirmationLinkException>(ConfirmAsync);

        Assert.Equal([InvalidLinkEventId], _fixture.Logger.LoggedEventIds());
    }

    [Fact]
    public async Task WrongValidatorIsInvalid()
    {
        _fixture.SetupStoredLink(_fixture.CreatePendingClient());

        await Assert.ThrowsAsync<InvalidConfirmationLinkException>(
            () => _fixture.CreateService().ConfirmAsync(_fixture.Selector, _fixture.Generator.GenerateValidator()));
    }

    [Fact]
    public async Task ChangedAccessCodeInvalidatesTheLink()
    {
        var client = _fixture.CreatePendingClient();
        _fixture.SetupStoredLink(client);
        client.AccessCode = "OutroCodigo1";

        await Assert.ThrowsAsync<InvalidConfirmationLinkException>(ConfirmAsync);
    }

    [Theory]
    [InlineData("curto", null)]
    [InlineData(null, "curto")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA=", null)]
    public async Task MalformedValuesAreInvalidWithoutQueryingTheDatabase(string? selector, string? validator)
    {
        await Assert.ThrowsAsync<InvalidConfirmationLinkException>(
            () => _fixture.CreateService().ConfirmAsync(selector ?? _fixture.Selector, validator ?? _fixture.Validator));

        _fixture.LinkRepository.Verify(
            repository => repository.FindBySelectorWithClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task LogsNeverContainSelectorOrValidator()
    {
        _fixture.SetupStoredLink(_fixture.CreatePendingClient());
        await ConfirmAsync();

        Assert.DoesNotContain(_fixture.Logger.LoggedMessages(), message =>
            message.Contains(_fixture.Selector, StringComparison.Ordinal) || message.Contains(_fixture.Validator, StringComparison.Ordinal));
    }

    private Task<EmailConfirmationOutcome> ConfirmAsync() =>
        _fixture.CreateService().ConfirmAsync(_fixture.Selector, _fixture.Validator);

    private void VerifySaved(Times times) =>
        _fixture.UnitOfWorkMocks.UnitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
}

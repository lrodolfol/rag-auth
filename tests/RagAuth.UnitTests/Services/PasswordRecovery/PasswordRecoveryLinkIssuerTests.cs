using Microsoft.Extensions.Options;
using Moq;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.PasswordRecovery;
using RagAuth.Api.Services.SecureLinks;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.PasswordRecovery;

public sealed class PasswordRecoveryLinkIssuerTests
{
    private readonly RandomSecureLinkGenerator _generator = new();
    private readonly Mock<IPasswordRecoveryLinkRepository> _linkRepository = new();
    private readonly AdjustableTimeProvider _timeProvider = new();
    private readonly Client _client = new() { Id = 42, AccessCode = "Ab3dEf6hIj9k", Name = "Ana", Email = "ana@exemplo.com", EmailConfirmed = true };

    [Fact]
    public void IssueCreatesLinkValidForTwentyFourHoursWithRecoveryPurpose()
    {
        var issuedLink = CreateIssuer().Issue(_client);

        var link = issuedLink.Entity;
        Assert.Equal(_timeProvider.GetUtcNow(), link.IssuedAt);
        Assert.Equal(link.IssuedAt + TimeSpan.FromHours(24), link.ExpiresAt);
        Assert.Equal("password-recovery", link.Purpose);
        Assert.Same(_client, link.Client);
        Assert.Equal(22, link.Selector.Length);
        _linkRepository.Verify(repository => repository.Add(link), Times.Once());
    }

    [Theory]
    [InlineData("https://ragauth.com.br")]
    [InlineData("https://ragauth.com.br/")]
    public void IssueBuildsRecoveryMessageWithUrlWithoutDoubleSlash(string baseUrl)
    {
        var issuedLink = CreateIssuer(baseUrl).Issue(_client);

        var message = issuedLink.Message;
        Assert.Equal(EmailKind.PasswordRecovery, message.Kind);
        Assert.Equal((42, "ana@exemplo.com", "Ana"), (message.ClientId, message.RecipientEmail, message.ClientName));
        Assert.Matches($"^https://ragauth\\.com\\.br/{issuedLink.Entity.Selector}/pass-recuvery/[A-Za-z0-9_-]{{43}}$", message.ActionUrl);
        Assert.Null(message.OccurredAt);
    }

    [Fact]
    public void IssueStoresOnlyTheHashOfTheValidatorBoundToRecoveryPurpose()
    {
        var issuedLink = CreateIssuer().Issue(_client);
        var validator = issuedLink.Message.ActionUrl!.Split('/')[^1];

        Assert.Equal(_generator.ComputeValidatorHash(validator, _client.AccessCode, "password-recovery"), issuedLink.Entity.ValidatorHash);
        Assert.DoesNotContain(validator, issuedLink.Entity.ValidatorHash);
        Assert.DoesNotContain(validator, issuedLink.Entity.Selector);
    }

    private PasswordRecoveryLinkIssuer CreateIssuer(string baseUrl = "https://ragauth.com.br") => new(
        _generator,
        _linkRepository.Object,
        Options.Create(new EmailOptions { PasswordRecoveryPageBaseUrl = baseUrl }),
        _timeProvider);
}

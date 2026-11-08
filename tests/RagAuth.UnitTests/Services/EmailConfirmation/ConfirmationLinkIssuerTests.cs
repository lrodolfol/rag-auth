using Microsoft.Extensions.Options;
using Moq;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.EmailConfirmation;

public sealed class ConfirmationLinkIssuerTests
{
    private readonly RandomConfirmationLinkGenerator _generator = new();
    private readonly Mock<IEmailConfirmationLinkRepository> _linkRepository = new();
    private readonly AdjustableTimeProvider _timeProvider = new();
    private readonly Client _client = new() { Id = 42, AccessCode = "Ab3dEf6hIj9k", Name = "Ana", Email = "ana@exemplo.com" };

    [Fact]
    public void IssueCreatesLinkValidForTwentyFourHoursWithOriginAndPurpose()
    {
        var issuedLink = CreateIssuer().Issue(_client, ConfirmationLinkOrigin.NewLinkRequest);

        var link = issuedLink.Entity;
        Assert.Equal(_timeProvider.GetUtcNow(), link.IssuedAt);
        Assert.Equal(link.IssuedAt + TimeSpan.FromHours(24), link.ExpiresAt);
        Assert.Equal(ConfirmationLinkOrigin.NewLinkRequest, link.Origin);
        Assert.Equal("email-confirmation", link.Purpose);
        Assert.Same(_client, link.Client);
        _linkRepository.Verify(repository => repository.Add(link), Times.Once());
    }

    [Fact]
    public void IssueStoresOnlyTheHashOfTheValidator()
    {
        var issuedLink = CreateIssuer().Issue(_client, ConfirmationLinkOrigin.Registration);

        Assert.Equal(_generator.ComputeValidatorHash(issuedLink.Validator, _client.AccessCode), issuedLink.Entity.ValidatorHash);
        Assert.DoesNotContain(issuedLink.Validator, issuedLink.Entity.ValidatorHash);
        Assert.NotEqual(issuedLink.Validator, issuedLink.Entity.Selector);
    }

    [Theory]
    [InlineData("https://ragauth.com.br")]
    [InlineData("https://ragauth.com.br/")]
    public void IssueBuildsFrontEndUrlWithoutDoubleSlash(string baseUrl)
    {
        var issuedLink = CreateIssuer(baseUrl).Issue(_client, ConfirmationLinkOrigin.Registration);

        var expectedUrl = $"https://ragauth.com.br/{issuedLink.Entity.Selector}/confirmation-register/{issuedLink.Validator}";
        Assert.Equal(expectedUrl, issuedLink.Message.ConfirmationUrl);
        Assert.Equal(new ConfirmationEmailMessage(42, "ana@exemplo.com", "Ana", expectedUrl, 1), issuedLink.Message);
    }

    [Fact]
    public void RehashForAccessCodeChangesOnlyTheHash()
    {
        var issuer = CreateIssuer();
        var issuedLink = issuer.Issue(_client, ConfirmationLinkOrigin.Registration);
        var originalSelector = issuedLink.Entity.Selector;
        var originalUrl = issuedLink.Message.ConfirmationUrl;
        var originalHash = issuedLink.Entity.ValidatorHash;

        issuer.RehashForAccessCode(issuedLink, "NovoCodigo12");

        Assert.NotEqual(originalHash, issuedLink.Entity.ValidatorHash);
        Assert.Equal(_generator.ComputeValidatorHash(issuedLink.Validator, "NovoCodigo12"), issuedLink.Entity.ValidatorHash);
        Assert.Equal(originalSelector, issuedLink.Entity.Selector);
        Assert.Equal(originalUrl, issuedLink.Message.ConfirmationUrl);
    }

    private ConfirmationLinkIssuer CreateIssuer(string baseUrl = "https://ragauth.com.br") => new(
        _generator,
        _linkRepository.Object,
        Options.Create(new EmailOptions { ConfirmationPageBaseUrl = baseUrl }),
        _timeProvider);
}

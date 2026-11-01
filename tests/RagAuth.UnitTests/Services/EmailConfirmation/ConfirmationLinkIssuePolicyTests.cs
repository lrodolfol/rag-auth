using Microsoft.Extensions.Options;
using Moq;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.EmailConfirmation;

/// <summary>
/// Usa um repositório em memória com a mesma regra de janela do repositório real
/// (links de <c>new-link-request</c> emitidos estritamente depois do início da janela).
/// </summary>
public sealed class ConfirmationLinkIssuePolicyTests
{
    private const int ClientId = 42;

    private readonly AdjustableTimeProvider _timeProvider = new();
    private readonly List<EmailConfirmationLink> _links = [];
    private readonly Mock<IEmailConfirmationLinkRepository> _linkRepository = new();

    public ConfirmationLinkIssuePolicyTests()
    {
        _linkRepository.Setup(repository => repository.FindLatestIssuedAtAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _links.Count == 0 ? null : _links.Max(link => link.IssuedAt));
        _linkRepository.Setup(repository => repository.CountIssuedSinceAsync(ClientId, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, string origin, DateTimeOffset since, CancellationToken _) =>
                _links.Count(link => link.Origin == origin && link.IssuedAt > since));
    }

    [Fact]
    public async Task AccountWithoutLinksCanIssue()
    {
        Assert.True(await CanIssueAsync());
    }

    [Fact]
    public async Task LastLinkFourMinutesFiftyNineSecondsAgoBlocks()
    {
        AddLink(ConfirmationLinkOrigin.Registration, TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));

        Assert.False(await CanIssueAsync());
    }

    [Fact]
    public async Task LastLinkExactlyFiveMinutesAgoAllows()
    {
        AddLink(ConfirmationLinkOrigin.NewLinkRequest, TimeSpan.FromMinutes(5));

        Assert.True(await CanIssueAsync());
    }

    [Fact]
    public async Task NineNewLinkRequestsInLastDayAllowAndTenBlock()
    {
        AddNewLinkRequests(9);
        Assert.True(await CanIssueAsync());

        AddNewLinkRequests(1, startingAgo: TimeSpan.FromHours(1));
        Assert.False(await CanIssueAsync());
    }

    [Fact]
    public async Task RegistrationLinksDoNotCountTowardsDailyLimit()
    {
        AddNewLinkRequests(9);
        AddLink(ConfirmationLinkOrigin.Registration, TimeSpan.FromHours(12));

        Assert.True(await CanIssueAsync());
    }

    [Fact]
    public async Task LinkIssuedMoreThanTwentyFourHoursAgoLeavesTheWindow()
    {
        AddNewLinkRequests(9);
        AddLink(ConfirmationLinkOrigin.NewLinkRequest, TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));

        Assert.True(await CanIssueAsync());
    }

    [Fact]
    public async Task ConfiguredValuesAreRespected()
    {
        AddLink(ConfirmationLinkOrigin.NewLinkRequest, TimeSpan.FromMinutes(1));

        Assert.True(await CanIssueAsync(new EmailOptions { NewLinkIntervalMinutes = 0, MaximumNewLinksPerDay = 2 }));
        Assert.False(await CanIssueAsync(new EmailOptions { NewLinkIntervalMinutes = 0, MaximumNewLinksPerDay = 1 }));
    }

    private void AddNewLinkRequests(int count, TimeSpan? startingAgo = null)
    {
        for (var index = 0; index < count; index++)
        {
            AddLink(ConfirmationLinkOrigin.NewLinkRequest, (startingAgo ?? TimeSpan.FromHours(2)) + TimeSpan.FromMinutes(10 * index));
        }
    }

    private void AddLink(string origin, TimeSpan issuedAgo) =>
        _links.Add(new EmailConfirmationLink { ClientId = ClientId, Origin = origin, IssuedAt = _timeProvider.GetUtcNow() - issuedAgo });

    private Task<bool> CanIssueAsync(EmailOptions? emailOptions = null) =>
        new ConfirmationLinkIssuePolicy(_linkRepository.Object, Options.Create(emailOptions ?? new EmailOptions()), _timeProvider)
            .CanIssueAsync(ClientId);
}

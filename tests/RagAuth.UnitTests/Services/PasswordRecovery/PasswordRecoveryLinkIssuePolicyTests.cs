using Microsoft.Extensions.Options;
using Moq;
using RagAuth.Api.Configuration;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.PasswordRecovery;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.PasswordRecovery;

public sealed class PasswordRecoveryLinkIssuePolicyTests
{
    private const int ClientId = 42;

    private readonly Mock<IPasswordRecoveryLinkRepository> _linkRepository = new();
    private readonly AdjustableTimeProvider _timeProvider = new();

    [Fact]
    public async Task WithoutPreviousLinksIssueIsAllowed()
    {
        Assert.True(await CreatePolicy().CanIssueAsync(ClientId));
    }

    [Fact]
    public async Task LinkIssuedLessThanFiveMinutesAgoBlocksIssue()
    {
        SetupLatestIssuedAt(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));

        Assert.False(await CreatePolicy().CanIssueAsync(ClientId));
    }

    [Fact]
    public async Task LinkIssuedExactlyFiveMinutesAgoAllowsIssue()
    {
        SetupLatestIssuedAt(TimeSpan.FromMinutes(5));

        Assert.True(await CreatePolicy().CanIssueAsync(ClientId));
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public async Task DailyLimitCountsLinksOfTheLastTwentyFourHours(int issuedInWindow, bool expected)
    {
        SetupLatestIssuedAt(TimeSpan.FromHours(1));
        var windowStart = _timeProvider.GetUtcNow() - TimeSpan.FromHours(24);
        _linkRepository.Setup(repository => repository.CountIssuedSinceAsync(ClientId, windowStart, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuedInWindow);

        Assert.Equal(expected, await CreatePolicy().CanIssueAsync(ClientId));
    }

    [Fact]
    public async Task LimitsComeFromRecoveryOptionsAndNotFromConfirmationOptions()
    {
        SetupLatestIssuedAt(TimeSpan.FromMinutes(2));
        _linkRepository.Setup(repository => repository.CountIssuedSinceAsync(ClientId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var options = new EmailOptions
        {
            PasswordRecoveryIntervalMinutes = 1,
            MaximumPasswordRecoveryLinksPerDay = 3,
            NewLinkIntervalMinutes = 60,
            MaximumNewLinksPerDay = 1,
        };

        Assert.True(await CreatePolicy(options).CanIssueAsync(ClientId));
    }

    private void SetupLatestIssuedAt(TimeSpan age) =>
        _linkRepository.Setup(repository => repository.FindLatestIssuedAtAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_timeProvider.GetUtcNow() - age);

    private PasswordRecoveryLinkIssuePolicy CreatePolicy(EmailOptions? options = null) =>
        new(_linkRepository.Object, Options.Create(options ?? new EmailOptions()), _timeProvider);
}

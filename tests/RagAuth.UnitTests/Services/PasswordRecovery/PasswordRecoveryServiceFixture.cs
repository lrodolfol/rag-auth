using Microsoft.Extensions.Logging;
using Moq;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.Auth;
using RagAuth.Api.Services.PasswordRecovery;
using RagAuth.Api.Services.SecureLinks;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.PasswordRecovery;

/// <summary>Dublês compartilhados pelos testes de <see cref="PasswordRecoveryService"/>.</summary>
public sealed class PasswordRecoveryServiceFixture
{
    public const string AccessCode = "Ab3dEf6hIj9k";
    public const string NewPasswordHash = "hash-da-nova-senha";

    public PasswordRecoveryServiceFixture()
    {
        Validator = Generator.GenerateValidator();
        Selector = Generator.GenerateSelector();
        EmailQueue.Setup(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>())).Returns(true);
        IssuePolicy.Setup(policy => policy.CanIssueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        PasswordHashing.Setup(hashing => hashing.HashPassword(It.IsAny<Client>(), It.IsAny<string>())).Returns(NewPasswordHash);
        DurationGuard.Setup(guard => guard.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((operation, cancellationToken) => operation(cancellationToken));
    }

    public RandomSecureLinkGenerator Generator { get; } = new();

    public string Selector { get; }

    public string Validator { get; }

    public Mock<IPasswordRecoveryLinkRepository> LinkRepository { get; } = new();

    public Mock<IClientRepository> ClientRepository { get; } = new();

    public Mock<IPasswordRecoveryLinkIssuer> LinkIssuer { get; } = new();

    public Mock<IPasswordRecoveryLinkIssuePolicy> IssuePolicy { get; } = new();

    public Mock<IEmailQueue> EmailQueue { get; } = new();

    public Mock<IPasswordHashingService> PasswordHashing { get; } = new();

    public Mock<IMinimumResponseDurationGuard> DurationGuard { get; } = new();

    public UnitOfWorkMocks UnitOfWorkMocks { get; } = new();

    public AdjustableTimeProvider TimeProvider { get; } = new();

    public Mock<ILogger<PasswordRecoveryService>> Logger { get; } = LoggerMocks.Create<PasswordRecoveryService>();

    public Client CreateConfirmedClient() => new()
    {
        Id = 42,
        AccessCode = AccessCode,
        Name = "Ana",
        Email = "ana@exemplo.com",
        EmailConfirmed = true,
        PasswordHash = "hash-da-senha-antiga",
    };

    public PasswordRecoveryLink SetupStoredLink(Client client, Action<PasswordRecoveryLink>? customize = null, string purpose = PasswordRecoveryLink.PasswordRecoveryPurpose)
    {
        var link = new PasswordRecoveryLink
        {
            ClientId = client.Id,
            Client = client,
            Selector = Selector,
            ValidatorHash = Generator.ComputeValidatorHash(Validator, client.AccessCode, purpose),
            IssuedAt = TimeProvider.GetUtcNow() - TimeSpan.FromHours(1),
            ExpiresAt = TimeProvider.GetUtcNow() + TimeSpan.FromHours(23),
        };
        customize?.Invoke(link);
        LinkRepository.Setup(repository => repository.FindBySelectorWithClientAsync(Selector, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);
        return link;
    }

    public PasswordRecoveryService CreateService() => new(
        LinkRepository.Object,
        ClientRepository.Object,
        Generator,
        LinkIssuer.Object,
        IssuePolicy.Object,
        EmailQueue.Object,
        PasswordHashing.Object,
        DurationGuard.Object,
        UnitOfWorkMocks.UnitOfWork.Object,
        TimeProvider,
        Logger.Object);
}

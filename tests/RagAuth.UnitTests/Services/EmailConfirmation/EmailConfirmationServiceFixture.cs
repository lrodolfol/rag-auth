using Microsoft.Extensions.Logging;
using Moq;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.Api.Services.SecureLinks;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.EmailConfirmation;

/// <summary>Dublês compartilhados pelos testes de <see cref="EmailConfirmationService"/>.</summary>
public sealed class EmailConfirmationServiceFixture
{
    public const string AccessCode = "Ab3dEf6hIj9k";

    public EmailConfirmationServiceFixture()
    {
        Validator = Generator.GenerateValidator();
        Selector = Generator.GenerateSelector();
        EmailQueue.Setup(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>())).Returns(true);
        IssuePolicy.Setup(policy => policy.CanIssueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public RandomSecureLinkGenerator Generator { get; } = new();

    public string Selector { get; }

    public string Validator { get; }

    public Mock<IEmailConfirmationLinkRepository> LinkRepository { get; } = new();

    public Mock<IClientRepository> ClientRepository { get; } = new();

    public Mock<IConfirmationLinkIssuer> LinkIssuer { get; } = new();

    public Mock<IConfirmationLinkIssuePolicy> IssuePolicy { get; } = new();

    public Mock<IEmailQueue> EmailQueue { get; } = new();

    public UnitOfWorkMocks UnitOfWorkMocks { get; } = new();

    public AdjustableTimeProvider TimeProvider { get; } = new();

    public Mock<ILogger<EmailConfirmationService>> Logger { get; } = LoggerMocks.Create<EmailConfirmationService>();

    public Client CreatePendingClient() =>
        new() { Id = 42, AccessCode = AccessCode, Name = "Ana", Email = "ana@exemplo.com", EmailConfirmed = false };

    public EmailConfirmationLink SetupStoredLink(Client client, Action<EmailConfirmationLink>? customize = null)
    {
        var link = new EmailConfirmationLink
        {
            ClientId = client.Id,
            Client = client,
            Selector = Selector,
            ValidatorHash = Generator.ComputeValidatorHash(Validator, client.AccessCode, EmailConfirmationLink.EmailConfirmationPurpose),
            IssuedAt = TimeProvider.GetUtcNow() - TimeSpan.FromHours(1),
            ExpiresAt = TimeProvider.GetUtcNow() + TimeSpan.FromHours(23),
        };
        customize?.Invoke(link);
        LinkRepository.Setup(repository => repository.FindBySelectorWithClientAsync(Selector, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);
        return link;
    }

    public EmailConfirmationService CreateService() => new(
        LinkRepository.Object,
        ClientRepository.Object,
        Generator,
        LinkIssuer.Object,
        IssuePolicy.Object,
        EmailQueue.Object,
        UnitOfWorkMocks.UnitOfWork.Object,
        TimeProvider,
        Logger.Object);
}

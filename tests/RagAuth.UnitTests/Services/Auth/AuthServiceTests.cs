using Microsoft.Extensions.Logging;
using Moq;
using RagAuth.Api.Contracts.Auth;
using RagAuth.Api.Contracts.Business;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.Auth;
using RagAuth.Api.Services.Business;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Services.Auth;

public sealed class AuthServiceTests
{
    private readonly Mock<IClientRepository> _clientRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPasswordHashingService> _passwordHashingService = new();
    private readonly Mock<IAccessCodeGenerator> _accessCodeGenerator = new();
    private readonly Mock<IAccessTokenIssuer> _accessTokenIssuer = new();
    private readonly Mock<IBusinessService> _businessService = new();
    private readonly Mock<IConfirmationLinkIssuer> _confirmationLinkIssuer = new();
    private readonly Mock<IEmailQueue> _confirmationEmailQueue = new();
    private readonly Mock<ILogger<AuthService>> _logger = LoggerMocks.Create<AuthService>();
    private readonly List<string> _callOrder = [];
    private Client? _addedClient;
    private IssuedConfirmationLink? _issuedLink;

    public AuthServiceTests()
    {
        _clientRepository.Setup(repository => repository.Add(It.IsAny<Client>())).Callback<Client>(client => _addedClient = client);
        _passwordHashingService.Setup(service => service.HashPassword(It.IsAny<Client>(), It.IsAny<string>())).Returns("hash");
        _accessCodeGenerator.SetupSequence(generator => generator.Generate())
            .Returns("CODIGO000001").Returns("CODIGO000002").Returns("CODIGO000003")
            .Returns("CODIGO000004").Returns("CODIGO000005").Returns("CODIGO000006");
        _confirmationLinkIssuer.Setup(issuer => issuer.Issue(It.IsAny<Client>(), It.IsAny<string>()))
            .Returns<Client, string>(CreateIssuedLink);
        _confirmationEmailQueue.Setup(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>()))
            .Callback(() => _callOrder.Add("enqueue"))
            .Returns(true);
        _unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _callOrder.Add("save"))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task RegisterCreatesPendingAccountWithRegistrationLinkBeforeSaving()
    {
        _clientRepository.Setup(repository => repository.Add(It.IsAny<Client>())).Callback<Client>(client =>
        {
            client.Id = 42;
            _addedClient = client;
        });

        await CreateService().RegisterAsync(CreateRegisterRequest());

        Assert.False(_addedClient!.EmailConfirmed);
        Assert.Null(_addedClient.EmailConfirmedAt);
        _confirmationLinkIssuer.Verify(issuer => issuer.Issue(_addedClient, ConfirmationLinkOrigin.Registration), Times.Once());
        Assert.Equal(["issue", "save", "enqueue"], _callOrder);
        _confirmationEmailQueue.Verify(queue => queue.TryEnqueue(It.Is<OutgoingEmailMessage>(message =>
            message.ClientId == 42 && message.Kind == EmailKind.EmailConfirmation && message.ActionUrl == _issuedLink!.Message.ActionUrl)), Times.Once());
    }

    [Fact]
    public async Task RegisterDoesNotEnqueueEmailWhenSaveFails()
    {
        _unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolationExceptions.For(UniqueIndexNames.ActiveClientEmail));

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() => CreateService().RegisterAsync(CreateRegisterRequest()));

        _confirmationEmailQueue.Verify(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>()), Times.Never());
    }

    [Fact]
    public async Task RegisterRehashesLinkWhenAccessCodeIsRegenerated()
    {
        _unitOfWork.SetupSequence(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolationExceptions.For(UniqueIndexNames.ClientAccessCode))
            .Returns(Task.CompletedTask);

        await CreateService().RegisterAsync(CreateRegisterRequest());

        _confirmationLinkIssuer.Verify(issuer => issuer.RehashForAccessCode(_issuedLink!, "CODIGO000002"), Times.Once());
    }

    [Fact]
    public async Task RegisterSucceedsAndLogsErrorWhenEmailQueueIsFull()
    {
        _confirmationEmailQueue.Setup(queue => queue.TryEnqueue(It.IsAny<OutgoingEmailMessage>())).Returns(false);

        var response = await CreateService().RegisterAsync(CreateRegisterRequest());

        Assert.Equal("CODIGO000001", response.AccessCode);
        Assert.Contains(1509, _logger.LoggedEventIds());
    }

    [Fact]
    public async Task LoginOfPendingAccountWithCorrectPasswordIsForbiddenWithoutToken()
    {
        var client = SetupExistingClient(passwordIsValid: true);
        client.EmailConfirmed = false;

        await Assert.ThrowsAsync<EmailNotConfirmedException>(() => CreateService().LoginAsync(CreateLoginRequest()));

        _accessTokenIssuer.Verify(issuer => issuer.Issue(It.IsAny<Client>()), Times.Never());
        Assert.Equal([1506], _logger.LoggedEventIds());
    }

    [Fact]
    public async Task LoginOfPendingAccountWithWrongPasswordKeepsGenericError()
    {
        var client = SetupExistingClient(passwordIsValid: false);
        client.EmailConfirmed = false;

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => CreateService().LoginAsync(CreateLoginRequest()));

        Assert.Equal([1101], _logger.LoggedEventIds());
    }

    [Fact]
    public async Task RegisterNormalizesEmailAndNameAndReturnsClientData()
    {
        var response = await CreateService().RegisterAsync(CreateRegisterRequest("  Ana@Exemplo.COM ", "  Ana Souza "));

        Assert.Equal("ana@exemplo.com", _addedClient!.Email);
        Assert.Equal("Ana Souza", _addedClient.Name);
        Assert.Equal("hash", _addedClient.PasswordHash);
        Assert.Equal("CODIGO000001", response.AccessCode);
        Assert.Equal("ana@exemplo.com", response.Email);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task RegisterRejectsEmailOfExistingActiveAccount()
    {
        _clientRepository.Setup(repository => repository.FindActiveByEmailAsync("ana@exemplo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Client());

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() => CreateService().RegisterAsync(CreateRegisterRequest()));

        _clientRepository.Verify(repository => repository.Add(It.IsAny<Client>()), Times.Never());
    }

    [Fact]
    public async Task RegisterTranslatesActiveEmailIndexViolationToConflict()
    {
        _unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolationExceptions.For(UniqueIndexNames.ActiveClientEmail));

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() => CreateService().RegisterAsync(CreateRegisterRequest()));
    }

    [Fact]
    public async Task RegisterRetriesWithNewAccessCodeWhenCodeIsTaken()
    {
        _unitOfWork.SetupSequence(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolationExceptions.For(UniqueIndexNames.ClientAccessCode))
            .Returns(Task.CompletedTask);

        var response = await CreateService().RegisterAsync(CreateRegisterRequest());

        Assert.Equal("CODIGO000002", response.AccessCode);
    }

    [Fact]
    public async Task RegisterFailsAfterFiveAccessCodeCollisions()
    {
        _unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolationExceptions.For(UniqueIndexNames.ClientAccessCode));

        await Assert.ThrowsAsync<AccessCodeGenerationException>(() => CreateService().RegisterAsync(CreateRegisterRequest()));

        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    [Fact]
    public async Task LoginRejectsUnknownOrRemovedAccountWithGenericError()
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => CreateService().LoginAsync(CreateLoginRequest()));

        _logger.VerifyLogged(LogLevel.Warning, Times.Once());
        Assert.DoesNotContain(_logger.LoggedMessages(), message => message.Contains("ana@exemplo.com") || message.Contains("senhaSegura1"));
    }

    [Fact]
    public async Task LoginRejectsWrongPasswordWithGenericError()
    {
        SetupExistingClient(passwordIsValid: false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => CreateService().LoginAsync(CreateLoginRequest()));

        Assert.Contains(_logger.LoggedMessages(), message => message.Contains("a***@exemplo.com"));
    }

    [Fact]
    public async Task LoginReturnsTokenClientDataAndOwnedBusiness()
    {
        var client = SetupExistingClient(passwordIsValid: true);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var business = new BusinessResponse("Padaria", "Pães", null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _accessTokenIssuer.Setup(issuer => issuer.Issue(client)).Returns(new IssuedAccessToken("token", expiresAt));
        _businessService.Setup(service => service.FindOwnedBusinessAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(business);

        var response = await CreateService().LoginAsync(CreateLoginRequest(" ANA@exemplo.com "));

        Assert.Equal(new LoginResponse("token", "Bearer", expiresAt, 42, "Ab3dEf6hIj9k", business), response);
    }

    [Fact]
    public async Task LoginReturnsNullBusinessWhenClientHasNone()
    {
        var client = SetupExistingClient(passwordIsValid: true);
        _accessTokenIssuer.Setup(issuer => issuer.Issue(client)).Returns(new IssuedAccessToken("token", DateTimeOffset.UtcNow));

        var response = await CreateService().LoginAsync(CreateLoginRequest());

        Assert.Null(response.Business);
    }

    private Client SetupExistingClient(bool passwordIsValid)
    {
        var client = new Client { Id = 42, AccessCode = "Ab3dEf6hIj9k", Email = "ana@exemplo.com", PasswordHash = "hash", EmailConfirmed = true };
        _clientRepository.Setup(repository => repository.FindActiveByEmailAsync("ana@exemplo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _passwordHashingService.Setup(service => service.VerifyPassword(client, "senhaSegura1")).Returns(passwordIsValid);
        return client;
    }

    private IssuedConfirmationLink CreateIssuedLink(Client client, string origin)
    {
        _callOrder.Add("issue");
        var link = new EmailConfirmationLink { Client = client, Origin = origin };
        var message = new OutgoingEmailMessage(EmailKind.EmailConfirmation, client.Id, client.Email, client.Name, "https://ragauth.com.br/s/confirmation-register/v");
        _issuedLink = new IssuedConfirmationLink(link, "validator", message);
        return _issuedLink;
    }

    private static RegisterClientRequest CreateRegisterRequest(string email = "ana@exemplo.com", string name = "Ana") =>
        new() { Name = name, Email = email, Password = "senhaSegura1" };

    private static LoginRequest CreateLoginRequest(string email = "ana@exemplo.com") =>
        new() { Email = email, Password = "senhaSegura1" };

    private AuthService CreateService() => new(
        _clientRepository.Object,
        _unitOfWork.Object,
        _passwordHashingService.Object,
        _accessCodeGenerator.Object,
        _accessTokenIssuer.Object,
        _businessService.Object,
        _confirmationLinkIssuer.Object,
        _confirmationEmailQueue.Object,
        _logger.Object);
}

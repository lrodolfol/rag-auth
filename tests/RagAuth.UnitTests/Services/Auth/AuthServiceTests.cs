using Microsoft.Extensions.Logging;
using Moq;
using RagAuth.Api.Contracts.Auth;
using RagAuth.Api.Contracts.Business;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.Auth;
using RagAuth.Api.Services.Business;
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
    private readonly Mock<ILogger<AuthService>> _logger = LoggerMocks.Create<AuthService>();
    private Client? _addedClient;

    public AuthServiceTests()
    {
        _clientRepository.Setup(repository => repository.Add(It.IsAny<Client>())).Callback<Client>(client => _addedClient = client);
        _passwordHashingService.Setup(service => service.HashPassword(It.IsAny<Client>(), It.IsAny<string>())).Returns("hash");
        _accessCodeGenerator.SetupSequence(generator => generator.Generate())
            .Returns("CODIGO000001").Returns("CODIGO000002").Returns("CODIGO000003")
            .Returns("CODIGO000004").Returns("CODIGO000005").Returns("CODIGO000006");
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
        var client = new Client { Id = 42, AccessCode = "Ab3dEf6hIj9k", Email = "ana@exemplo.com", PasswordHash = "hash" };
        _clientRepository.Setup(repository => repository.FindActiveByEmailAsync("ana@exemplo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _passwordHashingService.Setup(service => service.VerifyPassword(client, "senhaSegura1")).Returns(passwordIsValid);
        return client;
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
        _logger.Object);
}

using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.Api.Services;
using RagAuth.Api.Services.Clients;
using RagAuth.UnitTests.TestDoubles;
using BusinessEntity = RagAuth.Api.Domain.Entities.Business;

namespace RagAuth.UnitTests.Services.Clients;

public sealed class ClientAccountServiceTests
{
    private const int ClientId = 42;
    private const string AccessCode = "Ab3dEf6hIj9k";
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 25, 13, 0, 0, TimeSpan.Zero);

    private readonly Mock<IClientRepository> _clientRepository = new();
    private readonly Mock<IBusinessRepository> _businessRepository = new();
    private readonly Mock<IRagApiClient> _ragApiClient = new();
    private readonly Mock<ILogger<RagSynchronizedTransaction>> _transactionLogger = LoggerMocks.Create<RagSynchronizedTransaction>();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly UnitOfWorkMocks _unitOfWork = new();
    private readonly Client _client = new() { Id = ClientId, AccessCode = AccessCode };

    public ClientAccountServiceTests()
    {
        _clientRepository.Setup(repository => repository.FindActiveByIdAsync(ClientId, It.IsAny<CancellationToken>())).ReturnsAsync(_client);
        _timeProvider.Setup(provider => provider.GetUtcNow()).Returns(CurrentTime);
    }

    [Fact]
    public async Task DeletesBusinessInRagThenMarksAccountAsRemovedAndCommits()
    {
        var business = SetupExistingBusiness();

        await CreateService().DeleteAccountAsync(ClientId);

        _businessRepository.Verify(repository => repository.Remove(business));
        _ragApiClient.Verify(client => client.DeleteBusinessAsync(AccessCode, It.IsAny<CancellationToken>()));
        Assert.Equal(CurrentTime, _client.DeletedAt);
        _unitOfWork.UnitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()));
        _unitOfWork.VerifyCommitted();
    }

    [Fact]
    public async Task AccountWithoutBusinessDoesNotCallRag()
    {
        await CreateService().DeleteAccountAsync(ClientId);

        Assert.Empty(_ragApiClient.Invocations);
        _unitOfWork.VerifyCommitted();
    }

    [Fact]
    public async Task RagFailureRollsBackAndPropagates()
    {
        SetupExistingBusiness();
        _ragApiClient.Setup(client => client.DeleteBusinessAsync(AccessCode, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RagApiUnavailableException());

        await Assert.ThrowsAsync<RagApiUnavailableException>(() => CreateService().DeleteAccountAsync(ClientId));

        _unitOfWork.VerifyRolledBack();
    }

    [Fact]
    public async Task CommitFailureAfterRagSuccessIsLoggedAndPropagated()
    {
        SetupExistingBusiness();
        _unitOfWork.Transaction.Setup(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NpgsqlException("Conexão perdida."));

        await Assert.ThrowsAsync<NpgsqlException>(() => CreateService().DeleteAccountAsync(ClientId));

        _transactionLogger.VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task MissingAccountIsRejected()
    {
        _clientRepository.Reset();

        await Assert.ThrowsAsync<ClientNotFoundException>(() => CreateService().DeleteAccountAsync(ClientId));
    }

    private BusinessEntity SetupExistingBusiness()
    {
        var business = new BusinessEntity { ClientId = ClientId };
        _businessRepository.Setup(repository => repository.FindByClientIdAsync(ClientId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        return business;
    }

    private ClientAccountService CreateService() => new(
        _clientRepository.Object,
        _businessRepository.Object,
        _ragApiClient.Object,
        new RagSynchronizedTransaction(_unitOfWork.UnitOfWork.Object, _transactionLogger.Object),
        _timeProvider.Object,
        LoggerMocks.Create<ClientAccountService>().Object);
}

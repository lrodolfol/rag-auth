using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using RagAuth.Api.Contracts.Business;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.Api.Services;
using RagAuth.Api.Services.Business;
using RagAuth.UnitTests.TestDoubles;
using BusinessEntity = RagAuth.Api.Domain.Entities.Business;

namespace RagAuth.UnitTests.Services.Business;

public sealed class BusinessServiceCompensationTests
{
    private const int ClientId = 42;
    private const string AccessCode = "Ab3dEf6hIj9k";

    private readonly Mock<IBusinessRepository> _businessRepository = new();
    private readonly Mock<IClientRepository> _clientRepository = new();
    private readonly Mock<IRagApiClient> _ragApiClient = new();
    private readonly Mock<ILogger<RagSynchronizedTransaction>> _logger = LoggerMocks.Create<RagSynchronizedTransaction>();
    private readonly UnitOfWorkMocks _unitOfWork = new();
    private readonly BusinessRequest _request = new() { Name = "Padaria", Description = "Pães" };

    public BusinessServiceCompensationTests()
    {
        _clientRepository.Setup(repository => repository.FindActiveByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Client { Id = ClientId, AccessCode = AccessCode });
        _unitOfWork.Transaction.Setup(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NpgsqlException("Conexão perdida."));
    }

    [Fact]
    public async Task CommitFailureAfterRagCreationLogsAndCompensatesWithDelete()
    {
        var exception = await Record.ExceptionAsync(() => CreateService().CreateOwnedBusinessAsync(ClientId, _request));

        Assert.IsType<NpgsqlException>(exception);
        _logger.VerifyLogged(LogLevel.Error, Times.Once());
        _ragApiClient.Verify(client => client.DeleteBusinessAsync(AccessCode, CancellationToken.None), Times.Once());
    }

    [Fact]
    public async Task CompensationFailureIsLoggedWithoutMaskingOriginalFailure()
    {
        _ragApiClient.Setup(client => client.DeleteBusinessAsync(AccessCode, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("RAG fora do ar."));

        var exception = await Record.ExceptionAsync(() => CreateService().CreateOwnedBusinessAsync(ClientId, _request));

        Assert.IsType<NpgsqlException>(exception);
        _logger.VerifyLogged(LogLevel.Error, Times.Exactly(2));
    }

    [Fact]
    public async Task CommitFailureAfterRagUpdateOrDeleteOnlyLogs()
    {
        _businessRepository.Setup(repository => repository.FindByClientIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessEntity { ClientId = ClientId, Name = "Antiga", Description = "Antiga" });
        var service = CreateService();

        await Assert.ThrowsAsync<NpgsqlException>(() => service.UpdateOwnedBusinessAsync(ClientId, _request));
        await Assert.ThrowsAsync<NpgsqlException>(() => service.DeleteOwnedBusinessAsync(ClientId));

        _logger.VerifyLogged(LogLevel.Error, Times.Exactly(2));
        _ragApiClient.Verify(client => client.DeleteBusinessAsync(AccessCode, It.IsAny<CancellationToken>()), Times.Once());
    }

    private BusinessService CreateService() => new(
        _businessRepository.Object,
        _clientRepository.Object,
        new BusinessInputNormalizer(),
        _ragApiClient.Object,
        new RagSynchronizedTransaction(_unitOfWork.UnitOfWork.Object, _logger.Object));
}

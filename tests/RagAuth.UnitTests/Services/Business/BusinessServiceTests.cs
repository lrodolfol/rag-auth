using Moq;
using RagAuth.Api.Contracts.Business;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.Api.Services;
using RagAuth.Api.Services.Business;
using RagAuth.UnitTests.TestDoubles;
using BusinessEntity = RagAuth.Api.Domain.Entities.Business;

namespace RagAuth.UnitTests.Services.Business;

public sealed class BusinessServiceTests
{
    private const int ClientId = 42;
    private const string AccessCode = "Ab3dEf6hIj9k";

    private readonly Mock<IBusinessRepository> _businessRepository = new();
    private readonly Mock<IClientRepository> _clientRepository = new();
    private readonly Mock<IRagApiClient> _ragApiClient = new();
    private readonly UnitOfWorkMocks _unitOfWork = new();
    private readonly BusinessRequest _request = new() { Name = " Padaria ", Description = "Pães", Telephone = "(11) 98765-4321" };

    public BusinessServiceTests()
    {
        _clientRepository.Setup(repository => repository.FindActiveByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Client { Id = ClientId, AccessCode = AccessCode });
    }

    [Fact]
    public async Task FindOwnedBusinessReturnsMappedBusinessOrNull()
    {
        var service = CreateService();
        Assert.Null(await service.FindOwnedBusinessAsync(ClientId));

        SetupExistingBusiness();
        var business = await service.FindOwnedBusinessAsync(ClientId);

        Assert.Equal("Antiga", business!.Name);
    }

    [Fact]
    public async Task CreateWithExistingBusinessIsRejectedWithoutCallingRag()
    {
        SetupExistingBusiness();

        await Assert.ThrowsAsync<BusinessAlreadyExistsException>(() => CreateService().CreateOwnedBusinessAsync(ClientId, _request));

        VerifyRagNeverCalled();
    }

    [Fact]
    public async Task CreateTranslatesClientIdIndexViolationWithoutCallingRag()
    {
        _unitOfWork.UnitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolationExceptions.For(UniqueIndexNames.BusinessClientId));

        await Assert.ThrowsAsync<BusinessAlreadyExistsException>(() => CreateService().CreateOwnedBusinessAsync(ClientId, _request));

        VerifyRagNeverCalled();
        _unitOfWork.VerifyRolledBack();
    }

    [Fact]
    public async Task CreateSavesCallsRagWithNormalizedDataAndCommits()
    {
        BusinessEntity? addedBusiness = null;
        _businessRepository.Setup(repository => repository.Add(It.IsAny<BusinessEntity>())).Callback<BusinessEntity>(business => addedBusiness = business);

        var response = await CreateService().CreateOwnedBusinessAsync(ClientId, _request);

        Assert.Equal(ClientId, addedBusiness!.ClientId);
        Assert.Equal("Padaria", response.Name);
        Assert.Equal("11987654321", response.Telephone);
        _ragApiClient.Verify(client => client.CreateBusinessAsync(
            AccessCode,
            new NormalizedBusinessData("Padaria", "Pães", null, "11987654321", null, null),
            It.IsAny<CancellationToken>()));
        _unitOfWork.VerifyCommitted();
    }

    [Fact]
    public async Task CreateRollsBackAndPropagatesRagFailure()
    {
        _ragApiClient.Setup(client => client.CreateBusinessAsync(AccessCode, It.IsAny<NormalizedBusinessData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RagApiIntegrationException());

        await Assert.ThrowsAsync<RagApiIntegrationException>(() => CreateService().CreateOwnedBusinessAsync(ClientId, _request));

        _unitOfWork.VerifyRolledBack();
    }

    [Fact]
    public async Task UpdateOrDeleteWithoutBusinessIsRejectedWithoutCallingRag()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessNotFoundException>(() => service.UpdateOwnedBusinessAsync(ClientId, _request));
        await Assert.ThrowsAsync<BusinessNotFoundException>(() => service.DeleteOwnedBusinessAsync(ClientId));

        VerifyRagNeverCalled();
    }

    [Fact]
    public async Task UpdateAppliesDataCallsRagAndCommits()
    {
        var business = SetupExistingBusiness();

        var response = await CreateService().UpdateOwnedBusinessAsync(ClientId, _request);

        Assert.Equal("Padaria", business.Name);
        Assert.Equal("Padaria", response.Name);
        _ragApiClient.Verify(client => client.UpdateBusinessAsync(AccessCode, It.IsAny<NormalizedBusinessData>(), It.IsAny<CancellationToken>()));
        _unitOfWork.VerifyCommitted();
    }

    [Fact]
    public async Task DeleteRemovesCallsRagAndCommits()
    {
        var business = SetupExistingBusiness();

        await CreateService().DeleteOwnedBusinessAsync(ClientId);

        _businessRepository.Verify(repository => repository.Remove(business));
        _ragApiClient.Verify(client => client.DeleteBusinessAsync(AccessCode, It.IsAny<CancellationToken>()));
        _unitOfWork.VerifyCommitted();
    }

    [Fact]
    public async Task UpdateAndDeleteRollBackWhenRagFails()
    {
        SetupExistingBusiness();
        _ragApiClient.Setup(client => client.UpdateBusinessAsync(AccessCode, It.IsAny<NormalizedBusinessData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RagApiUnavailableException());

        await Assert.ThrowsAsync<RagApiUnavailableException>(() => CreateService().UpdateOwnedBusinessAsync(ClientId, _request));

        _unitOfWork.VerifyRolledBack();
    }

    [Fact]
    public async Task MissingClientIsRejected()
    {
        _clientRepository.Reset();

        await Assert.ThrowsAsync<ClientNotFoundException>(() => CreateService().CreateOwnedBusinessAsync(ClientId, _request));
    }

    private BusinessEntity SetupExistingBusiness()
    {
        var business = new BusinessEntity { ClientId = ClientId, Name = "Antiga", Description = "Descrição antiga" };
        _businessRepository.Setup(repository => repository.FindByClientIdAsync(ClientId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        return business;
    }

    private void VerifyRagNeverCalled() => Assert.Empty(_ragApiClient.Invocations);

    private BusinessService CreateService() => new(
        _businessRepository.Object,
        _clientRepository.Object,
        new BusinessInputNormalizer(),
        _ragApiClient.Object,
        new RagSynchronizedTransaction(_unitOfWork.UnitOfWork.Object, LoggerMocks.Create<RagSynchronizedTransaction>().Object));
}

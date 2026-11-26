using Moq;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.Dashboard;
using BusinessEntity = RagAuth.Api.Domain.Entities.Business;

namespace RagAuth.UnitTests.Services.Dashboard;

public sealed class BusinessDashboardServiceTests
{
    private const int ClientId = 42;

    private readonly Mock<IClientRepository> _clientRepository = new();

    [Fact]
    public async Task ClientWithBusinessReturnsFoundMessageAndBusiness()
    {
        SetupClient(new BusinessEntity { Name = "Padaria", Description = "Pães" });

        var result = await CreateService().GetDashboardAsync(ClientId);

        Assert.Equal("Negócio encontrado com sucesso.", result.Message);
        Assert.Equal("Padaria", result.Dashboard.Business!.Name);
        Assert.Equal(ClientId, result.Dashboard.Client.Id);
    }

    [Fact]
    public async Task ClientWithoutBusinessReturnsGuidanceMessageAndNullBusiness()
    {
        SetupClient(business: null);

        var result = await CreateService().GetDashboardAsync(ClientId);

        Assert.Equal("Nenhum negócio cadastrado. Cadastre o seu negócio para vê-lo no painel.", result.Message);
        Assert.Null(result.Dashboard.Business);
        Assert.Equal("ana@exemplo.com", result.Dashboard.Client.Email);
    }

    [Fact]
    public async Task MissingClientThrowsClientNotFound()
    {
        await Assert.ThrowsAsync<ClientNotFoundException>(() => CreateService().GetDashboardAsync(ClientId));
    }

    private void SetupClient(BusinessEntity? business) =>
        _clientRepository.Setup(repository => repository.FindConfirmedWithBusinessByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Client { Id = ClientId, Name = "Ana", Email = "ana@exemplo.com", EmailConfirmed = true, Business = business });

    private BusinessDashboardService CreateService() => new(_clientRepository.Object);
}

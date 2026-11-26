using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Persistence.Repositories;

namespace RagAuth.Api.Services.Dashboard;

public sealed class BusinessDashboardService(IClientRepository clientRepository) : IBusinessDashboardService
{
    private const string BusinessFoundMessage = "Negócio encontrado com sucesso.";
    private const string BusinessMissingMessage = "Nenhum negócio cadastrado. Cadastre o seu negócio para vê-lo no painel.";

    public async Task<BusinessDashboardResult> GetDashboardAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.FindConfirmedWithBusinessByIdAsync(clientId, cancellationToken)
            ?? throw new ClientNotFoundException();
        var message = client.Business is null ? BusinessMissingMessage : BusinessFoundMessage;
        return new BusinessDashboardResult(message, BusinessDashboardMapper.ToResponse(client));
    }
}

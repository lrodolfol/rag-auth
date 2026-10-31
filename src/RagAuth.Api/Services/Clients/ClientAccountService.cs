using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.Api.Observability;

namespace RagAuth.Api.Services.Clients;

public sealed class ClientAccountService(
    IClientRepository clientRepository,
    IBusinessRepository businessRepository,
    IRagApiClient ragApiClient,
    IRagSynchronizedTransaction ragSynchronizedTransaction,
    TimeProvider timeProvider,
    ILogger<ClientAccountService> logger) : IClientAccountService
{
    public async Task DeleteAccountAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.FindActiveByIdAsync(clientId, cancellationToken) ?? throw new ClientNotFoundException();
        var business = await businessRepository.FindByClientIdAsync(clientId, cancellationToken);
        if (business is not null)
        {
            businessRepository.Remove(business);
        }

        client.DeletedAt = timeProvider.GetUtcNow();
        await ragSynchronizedTransaction.ExecuteAsync(
            new RagSynchronizedOperation(RagApiOperations.DeleteAccount, clientId, token => DeleteRagBusinessAsync(client, business, token)),
            cancellationToken);
        LogMessages.ClientAccountDeleted(logger, clientId);
    }

    private Task DeleteRagBusinessAsync(Client client, Domain.Entities.Business? business, CancellationToken cancellationToken) =>
        business is null ? Task.CompletedTask : ragApiClient.DeleteBusinessAsync(client.AccessCode, cancellationToken);
}

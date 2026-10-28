using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Contracts.Business;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Infrastructure.RagApi;
using BusinessEntity = RagAuth.Api.Domain.Entities.Business;

namespace RagAuth.Api.Services.Business;

public sealed class BusinessService(
    IBusinessRepository businessRepository,
    IClientRepository clientRepository,
    IBusinessInputNormalizer inputNormalizer,
    IRagApiClient ragApiClient,
    IRagSynchronizedTransaction ragSynchronizedTransaction) : IBusinessService
{
    public async Task<BusinessResponse?> FindOwnedBusinessAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var business = await businessRepository.FindByClientIdAsync(clientId, cancellationToken);
        return business is null ? null : BusinessMapper.ToResponse(business);
    }

    public async Task<BusinessResponse> CreateOwnedBusinessAsync(int clientId, BusinessRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureClientHasNoBusinessAsync(clientId, cancellationToken);
        var client = await FindClientAsync(clientId, cancellationToken);
        var businessData = inputNormalizer.Normalize(request);
        var business = new BusinessEntity { ClientId = clientId };
        ApplyBusinessData(business, businessData);
        businessRepository.Add(business);
        await ExecuteCreationAsync(client, businessData, cancellationToken);
        return BusinessMapper.ToResponse(business);
    }

    public async Task<BusinessResponse> UpdateOwnedBusinessAsync(int clientId, BusinessRequest request, CancellationToken cancellationToken = default)
    {
        var business = await FindExistingBusinessAsync(clientId, cancellationToken);
        var client = await FindClientAsync(clientId, cancellationToken);
        var businessData = inputNormalizer.Normalize(request);
        ApplyBusinessData(business, businessData);
        await ragSynchronizedTransaction.ExecuteAsync(
            new RagSynchronizedOperation(
                RagApiOperations.UpdateBusiness,
                clientId,
                token => ragApiClient.UpdateBusinessAsync(client.AccessCode, businessData, token)),
            cancellationToken);
        return BusinessMapper.ToResponse(business);
    }

    public async Task DeleteOwnedBusinessAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var business = await FindExistingBusinessAsync(clientId, cancellationToken);
        var client = await FindClientAsync(clientId, cancellationToken);
        businessRepository.Remove(business);
        await ragSynchronizedTransaction.ExecuteAsync(
            new RagSynchronizedOperation(
                RagApiOperations.DeleteBusiness,
                clientId,
                token => ragApiClient.DeleteBusinessAsync(client.AccessCode, token)),
            cancellationToken);
    }

    private async Task EnsureClientHasNoBusinessAsync(int clientId, CancellationToken cancellationToken)
    {
        if (await businessRepository.FindByClientIdAsync(clientId, cancellationToken) is not null)
        {
            throw new BusinessAlreadyExistsException();
        }
    }

    private async Task<BusinessEntity> FindExistingBusinessAsync(int clientId, CancellationToken cancellationToken) =>
        await businessRepository.FindByClientIdAsync(clientId, cancellationToken) ?? throw new BusinessNotFoundException();

    private async Task<Client> FindClientAsync(int clientId, CancellationToken cancellationToken) =>
        await clientRepository.FindActiveByIdAsync(clientId, cancellationToken) ?? throw new ClientNotFoundException();

    private async Task ExecuteCreationAsync(Client client, NormalizedBusinessData businessData, CancellationToken cancellationToken)
    {
        var operation = new RagSynchronizedOperation(
            RagApiOperations.CreateBusiness,
            client.Id,
            token => ragApiClient.CreateBusinessAsync(client.AccessCode, businessData, token),
            () => ragApiClient.DeleteBusinessAsync(client.AccessCode, CancellationToken.None));
        try
        {
            await ragSynchronizedTransaction.ExecuteAsync(operation, cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.IsViolationOf(exception, UniqueIndexNames.BusinessClientId))
        {
            throw new BusinessAlreadyExistsException();
        }
    }

    private static void ApplyBusinessData(BusinessEntity business, NormalizedBusinessData businessData)
    {
        business.Name = businessData.Name;
        business.Description = businessData.Description;
        business.Email = businessData.Email;
        business.Telephone = businessData.Telephone;
        business.Address = businessData.Address;
        business.Details = businessData.Details;
    }
}

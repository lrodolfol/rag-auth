namespace RagAuth.Api.Services.Clients;

public interface IClientAccountService
{
    Task DeleteAccountAsync(int clientId, CancellationToken cancellationToken = default);
}

namespace RagAuth.Api.Services.PasswordRecovery;

public interface IPasswordRecoveryLinkIssuePolicy
{
    Task<bool> CanIssueAsync(int clientId, CancellationToken cancellationToken = default);
}

using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Services.PasswordRecovery;

public interface IPasswordRecoveryLinkIssuer
{
    IssuedPasswordRecoveryLink Issue(Client client);
}

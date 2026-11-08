using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Services.EmailConfirmation;

public interface IConfirmationLinkIssuer
{
    IssuedConfirmationLink Issue(Client client, string origin);

    void RehashForAccessCode(IssuedConfirmationLink issuedLink, string newAccessCode);
}

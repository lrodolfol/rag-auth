using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.SecureLinks;

namespace RagAuth.Api.Services.PasswordRecovery;

public sealed class PasswordRecoveryLinkIssuer(
    ISecureLinkGenerator linkGenerator,
    IPasswordRecoveryLinkRepository linkRepository,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider) : IPasswordRecoveryLinkIssuer
{
    /// <summary>Segmento público do link e da rota; a grafia vem da entrada de planejamento (research R1).</summary>
    public const string RecoveryPathSegment = "pass-recuvery";

    public IssuedPasswordRecoveryLink Issue(Client client)
    {
        var selector = linkGenerator.GenerateSelector();
        var validator = linkGenerator.GenerateValidator();
        var link = CreateLink(client, selector, validator);
        linkRepository.Add(link);
        var message = new OutgoingEmailMessage(
            EmailKind.PasswordRecovery,
            client.Id,
            client.Email,
            client.Name,
            ActionUrl: BuildRecoveryUrl(selector, validator));
        return new IssuedPasswordRecoveryLink(link, message);
    }

    private PasswordRecoveryLink CreateLink(Client client, string selector, string validator)
    {
        var issuedAt = timeProvider.GetUtcNow();
        return new PasswordRecoveryLink
        {
            Client = client,
            ClientId = client.Id,
            Selector = selector,
            ValidatorHash = linkGenerator.ComputeValidatorHash(validator, client.AccessCode, PasswordRecoveryLink.PasswordRecoveryPurpose),
            Purpose = PasswordRecoveryLink.PasswordRecoveryPurpose,
            IssuedAt = issuedAt,
            ExpiresAt = issuedAt + PasswordRecoveryLink.ValidityPeriod,
        };
    }

    private string BuildRecoveryUrl(string selector, string validator)
    {
        var baseUrl = emailOptions.Value.PasswordRecoveryPageBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{selector}/{RecoveryPathSegment}/{validator}";
    }
}

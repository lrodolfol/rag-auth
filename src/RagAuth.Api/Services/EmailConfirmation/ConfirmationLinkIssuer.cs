using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence.Repositories;

namespace RagAuth.Api.Services.EmailConfirmation;

public sealed class ConfirmationLinkIssuer(
    IConfirmationLinkGenerator linkGenerator,
    IEmailConfirmationLinkRepository linkRepository,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider) : IConfirmationLinkIssuer
{
    public const string ConfirmationPathSegment = "confirmation-register";

    public IssuedConfirmationLink Issue(Client client, string origin)
    {
        var selector = linkGenerator.GenerateSelector();
        var validator = linkGenerator.GenerateValidator();
        var link = CreateLink(client, origin, selector, validator);
        linkRepository.Add(link);
        var message = new ConfirmationEmailMessage(client.Id, client.Email, client.Name, BuildConfirmationUrl(selector, validator));
        return new IssuedConfirmationLink(link, validator, message);
    }

    public void RehashForAccessCode(IssuedConfirmationLink issuedLink, string newAccessCode) =>
        issuedLink.Entity.ValidatorHash = linkGenerator.ComputeValidatorHash(issuedLink.Validator, newAccessCode);

    private EmailConfirmationLink CreateLink(Client client, string origin, string selector, string validator)
    {
        var issuedAt = timeProvider.GetUtcNow();
        return new EmailConfirmationLink
        {
            Client = client,
            Selector = selector,
            ValidatorHash = linkGenerator.ComputeValidatorHash(validator, client.AccessCode),
            Purpose = EmailConfirmationLink.EmailConfirmationPurpose,
            Origin = origin,
            IssuedAt = issuedAt,
            ExpiresAt = issuedAt + EmailConfirmationLink.ValidityPeriod,
        };
    }

    private string BuildConfirmationUrl(string selector, string validator)
    {
        var baseUrl = emailOptions.Value.ConfirmationPageBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{selector}/{ConfirmationPathSegment}/{validator}";
    }
}

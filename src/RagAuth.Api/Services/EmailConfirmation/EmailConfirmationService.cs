using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Observability;
using RagAuth.Api.Services.SecureLinks;

namespace RagAuth.Api.Services.EmailConfirmation;

public sealed class EmailConfirmationService(
    IEmailConfirmationLinkRepository linkRepository,
    IClientRepository clientRepository,
    ISecureLinkGenerator linkGenerator,
    IConfirmationLinkIssuer linkIssuer,
    IConfirmationLinkIssuePolicy issuePolicy,
    IEmailQueue emailQueue,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<EmailConfirmationService> logger) : IEmailConfirmationService
{
    public async Task<EmailConfirmationOutcome> ConfirmAsync(string selector, string validator, CancellationToken cancellationToken = default)
    {
        var link = await FindMatchingLinkAsync(selector, validator, cancellationToken);
        if (link.Client.EmailConfirmed || link.UsedAt is not null)
        {
            return EmailConfirmationOutcome.AlreadyConfirmed;
        }

        EnsureLinkIsActive(link);
        await MarkEmailAsConfirmedAsync(link, cancellationToken);
        return EmailConfirmationOutcome.Confirmed;
    }

    public async Task RequestNewLinkAsync(string email, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.FindPendingByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        if (client is null)
        {
            return;
        }

        var issuedLink = await TryIssueNewLinkAsync(client, cancellationToken);
        if (issuedLink is null)
        {
            LogMessages.NewConfirmationLinkThrottled(logger, client.Id);
            return;
        }

        LogMessages.NewConfirmationLinkIssued(logger, client.Id);
        EnqueueConfirmationEmail(issuedLink.Message);
    }

    // Formato, existência e hash são verificados juntos: qualquer falha é o mesmo "link inválido" (FR-010).
    private async Task<EmailConfirmationLink> FindMatchingLinkAsync(string selector, string validator, CancellationToken cancellationToken)
    {
        var link = SecureLinkFormat.IsValid(selector, validator)
            ? await linkRepository.FindBySelectorWithClientAsync(selector, cancellationToken)
            : null;
        if (link is null || !linkGenerator.IsValidatorMatch(validator, link.Client.AccessCode, EmailConfirmationLink.EmailConfirmationPurpose, link.ValidatorHash))
        {
            throw CreateInvalidLinkException();
        }

        return link;
    }

    private void EnsureLinkIsActive(EmailConfirmationLink link)
    {
        if (link.RevokedAt is not null)
        {
            throw CreateInvalidLinkException();
        }

        if (timeProvider.GetUtcNow() > link.ExpiresAt)
        {
            LogMessages.ExpiredConfirmationLinkUsed(logger, link.ClientId);
            throw new ExpiredConfirmationLinkException();
        }
    }

    private async Task MarkEmailAsConfirmedAsync(EmailConfirmationLink link, CancellationToken cancellationToken)
    {
        var confirmedAt = timeProvider.GetUtcNow();
        link.UsedAt = confirmedAt;
        link.Client.EmailConfirmed = true;
        link.Client.EmailConfirmedAt = confirmedAt;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogMessages.EmailConfirmed(logger, link.ClientId);
    }

    private InvalidConfirmationLinkException CreateInvalidLinkException()
    {
        LogMessages.InvalidConfirmationLinkUsed(logger);
        return new InvalidConfirmationLinkException();
    }

    // O bloqueio da linha do cliente serializa pedidos simultâneos: a política é avaliada já com os links
    // emitidos pelo pedido anterior. O índice "um link ativo por cliente" é a última barreira (research R5).
    private async Task<IssuedConfirmationLink?> TryIssueNewLinkAsync(Client client, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await clientRepository.LockForUpdateAsync(client.Id, cancellationToken);
            if (!await issuePolicy.CanIssueAsync(client.Id, cancellationToken))
            {
                return null;
            }

            await RevokeActiveLinkAsync(client.Id, cancellationToken);
            var issuedLink = linkIssuer.Issue(client, ConfirmationLinkOrigin.NewLinkRequest);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return issuedLink;
        }
        catch (DbUpdateException exception)
            when (UniqueConstraintViolation.IsViolationOf(exception, UniqueIndexNames.ActiveEmailConfirmationLinkPerClient))
        {
            return null;
        }
    }

    private async Task RevokeActiveLinkAsync(int clientId, CancellationToken cancellationToken)
    {
        var activeLink = await linkRepository.FindActiveForClientAsync(clientId, cancellationToken);
        if (activeLink is null)
        {
            return;
        }

        activeLink.RevokedAt = timeProvider.GetUtcNow();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void EnqueueConfirmationEmail(OutgoingEmailMessage message)
    {
        if (!emailQueue.TryEnqueue(message))
        {
            LogMessages.EmailQueueRejected(logger, EmailKind.EmailConfirmation, message.ClientId);
        }
    }
}

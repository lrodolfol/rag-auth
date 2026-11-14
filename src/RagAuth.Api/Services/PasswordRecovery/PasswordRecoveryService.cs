using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Observability;
using RagAuth.Api.Services.Auth;
using RagAuth.Api.Services.SecureLinks;

namespace RagAuth.Api.Services.PasswordRecovery;

public sealed class PasswordRecoveryService(
    IPasswordRecoveryLinkRepository linkRepository,
    IClientRepository clientRepository,
    ISecureLinkGenerator linkGenerator,
    IPasswordRecoveryLinkIssuer linkIssuer,
    IPasswordRecoveryLinkIssuePolicy issuePolicy,
    IEmailQueue emailQueue,
    IPasswordHashingService passwordHashingService,
    IMinimumResponseDurationGuard responseDurationGuard,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<PasswordRecoveryService> logger) : IPasswordRecoveryService
{
    public Task RequestRecoveryAsync(string email, CancellationToken cancellationToken = default) =>
        responseDurationGuard.RunAsync(token => HandleRecoveryRequestAsync(email, token), cancellationToken);

    public async Task VerifyLinkAsync(string selector, string validator, CancellationToken cancellationToken = default)
    {
        var link = await FindMatchingLinkAsync(selector, validator, cancellationToken);
        EnsureLinkIsUsable(link);
    }

    public async Task ResetPasswordAsync(string selector, string validator, string newPassword, CancellationToken cancellationToken = default)
    {
        var link = await FindMatchingLinkAsync(selector, validator, cancellationToken);
        EnsureLinkIsUsable(link);
        var changedAt = await ChangePasswordInTransactionAsync(link, newPassword, cancellationToken);
        LogMessages.PasswordReset(logger, link.ClientId);
        var client = link.Client;
        Enqueue(new OutgoingEmailMessage(EmailKind.PasswordChanged, client.Id, client.Email, client.Name, ActionUrl: null, OccurredAt: changedAt));
    }

    private async Task HandleRecoveryRequestAsync(string email, CancellationToken cancellationToken)
    {
        var client = await clientRepository.FindConfirmedByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        if (client is null)
        {
            LogMessages.PasswordRecoveryRequestedForIneligibleEmail(logger);
            return;
        }

        var issuedLink = await TryIssueLinkAsync(client, cancellationToken);
        if (issuedLink is null)
        {
            LogMessages.PasswordRecoveryThrottled(logger, client.Id);
            return;
        }

        LogMessages.PasswordRecoveryLinkIssued(logger, client.Id);
        Enqueue(issuedLink.Message);
    }

    // O bloqueio da linha do cliente serializa pedidos simultâneos; o índice "um link ativo por cliente"
    // é a última barreira e, se violado, equivale a limite atingido (research R8).
    private async Task<IssuedPasswordRecoveryLink?> TryIssueLinkAsync(Client client, CancellationToken cancellationToken)
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
            var issuedLink = linkIssuer.Issue(client);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return issuedLink;
        }
        catch (DbUpdateException exception)
            when (UniqueConstraintViolation.IsViolationOf(exception, UniqueIndexNames.ActivePasswordRecoveryLinkPerClient))
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

    // Formato, existência e hash são verificados juntos e antes do prazo: só quem tem o par correto descobre que o link expirou.
    private async Task<PasswordRecoveryLink> FindMatchingLinkAsync(string selector, string validator, CancellationToken cancellationToken)
    {
        var link = SecureLinkFormat.IsValid(selector, validator)
            ? await linkRepository.FindBySelectorWithClientAsync(selector, cancellationToken)
            : null;
        if (link is null || !linkGenerator.IsValidatorMatch(validator, link.Client.AccessCode, PasswordRecoveryLink.PasswordRecoveryPurpose, link.ValidatorHash))
        {
            throw CreateInvalidLinkException();
        }

        return link;
    }

    private void EnsureLinkIsUsable(PasswordRecoveryLink link)
    {
        if (link.UsedAt is not null || link.RevokedAt is not null || !link.Client.EmailConfirmed)
        {
            throw CreateInvalidLinkException();
        }

        if (timeProvider.GetUtcNow() > link.ExpiresAt)
        {
            LogMessages.ExpiredPasswordRecoveryLinkUsed(logger, link.ClientId);
            throw new ExpiredPasswordRecoveryLinkException();
        }
    }

    // Relê o link depois do bloqueio: de dois PUT simultâneos, só o primeiro troca a senha (research R9).
    private async Task<DateTimeOffset> ChangePasswordInTransactionAsync(PasswordRecoveryLink link, string newPassword, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await clientRepository.LockForUpdateAsync(link.ClientId, cancellationToken);
        await linkRepository.ReloadAsync(link, cancellationToken);
        if (link.UsedAt is not null || link.RevokedAt is not null)
        {
            throw CreateInvalidLinkException();
        }

        var changedAt = ApplyNewPassword(link, newPassword);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changedAt;
    }

    private DateTimeOffset ApplyNewPassword(PasswordRecoveryLink link, string newPassword)
    {
        var changedAt = timeProvider.GetUtcNow();
        link.Client.PasswordHash = passwordHashingService.HashPassword(link.Client, newPassword);
        link.Client.PasswordChangedAt = changedAt;
        link.UsedAt = changedAt;
        return changedAt;
    }

    private void Enqueue(OutgoingEmailMessage message)
    {
        if (!emailQueue.TryEnqueue(message))
        {
            LogMessages.EmailQueueRejected(logger, message.Kind, message.ClientId);
        }
    }

    private InvalidPasswordRecoveryLinkException CreateInvalidLinkException()
    {
        LogMessages.InvalidPasswordRecoveryLinkUsed(logger);
        return new InvalidPasswordRecoveryLinkException();
    }
}

namespace RagAuth.Api.Infrastructure.Email;

/// <summary>
/// Mensagem da fila em memória. <see cref="ActionUrl"/> contém o validator: nunca é persistida nem registrada em log.
/// </summary>
public sealed record OutgoingEmailMessage(
    EmailKind Kind,
    int ClientId,
    string RecipientEmail,
    string ClientName,
    string? ActionUrl,
    DateTimeOffset? OccurredAt = null,
    int AttemptNumber = 1);

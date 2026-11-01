namespace RagAuth.Api.Services.EmailConfirmation;

/// <summary>
/// Mensagem da fila em memória. A URL contém o validator: nunca é persistida nem registrada em log.
/// </summary>
public sealed record ConfirmationEmailMessage(
    int ClientId,
    string RecipientEmail,
    string ClientName,
    string ConfirmationUrl,
    int AttemptNumber = 1);

namespace RagAuth.Api.Observability;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Ocorreu um erro inesperado ao processar a requisição.")]
    public static partial void UnexpectedErrorOccurred(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Cliente {ClientId} cadastrado com sucesso.")]
    public static partial void ClientRegistered(ILogger logger, int clientId);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "Tentativa de login malsucedida para o e-mail {MaskedEmail}.")]
    public static partial void LoginFailed(ILogger logger, string maskedEmail);

    [LoggerMessage(EventId = 1200, Level = LogLevel.Warning, Message = "Autenticação rejeitada: {Reason}.")]
    public static partial void AuthenticationRejected(ILogger logger, string reason);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Warning, Message = "Acesso negado ao cliente {ClientId} no caminho {RequestPath}.")]
    public static partial void AccessForbidden(ILogger logger, string? clientId, string requestPath);

    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "API RAG confirmou a {Operation} com status {StatusCode}.")]
    public static partial void RagApiCallSucceeded(ILogger logger, string operation, int statusCode);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Error, Message = "Falha ao confirmar no banco a {Operation} do cliente {ClientId} depois do sucesso na API RAG.")]
    public static partial void CommitFailedAfterRagSuccess(ILogger logger, string operation, int clientId, Exception exception);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Warning, Message = "API RAG recusou a {Operation} com status {StatusCode}.")]
    public static partial void RagApiRejectedRequest(ILogger logger, string operation, int statusCode);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Error, Message = "API RAG rejeitou a chave de acesso na {Operation} com status {StatusCode}.")]
    public static partial void RagApiKeyRejected(ILogger logger, string operation, int statusCode);

    [LoggerMessage(EventId = 1304, Level = LogLevel.Error, Message = "Falha na chamada à API RAG durante a {Operation}: {FailureDescription}.")]
    public static partial void RagApiFailed(ILogger logger, string operation, string failureDescription);

    [LoggerMessage(EventId = 1305, Level = LogLevel.Error, Message = "Falha na compensação da {Operation} do cliente {ClientId} na API RAG.")]
    public static partial void RagCompensationFailed(ILogger logger, string operation, int clientId, Exception exception);

    [LoggerMessage(EventId = 1400, Level = LogLevel.Information, Message = "Conta do cliente {ClientId} removida com sucesso.")]
    public static partial void ClientAccountDeleted(ILogger logger, int clientId);

    [LoggerMessage(EventId = 1500, Level = LogLevel.Information, Message = "E-mail de confirmação do cliente {ClientId} enviado na tentativa {AttemptNumber}.")]
    public static partial void ConfirmationEmailSent(ILogger logger, int clientId, int attemptNumber);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Warning, Message = "Falha na tentativa {AttemptNumber} de envio do e-mail de confirmação do cliente {ClientId}: {FailureType}.")]
    public static partial void ConfirmationEmailAttemptFailed(ILogger logger, int clientId, int attemptNumber, string failureType);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Error, Message = "O envio do e-mail de confirmação do cliente {ClientId} esgotou as {AttemptCount} tentativas.")]
    public static partial void ConfirmationEmailDeliveryExhausted(ILogger logger, int clientId, int attemptCount);

    [LoggerMessage(EventId = 1503, Level = LogLevel.Information, Message = "E-mail do cliente {ClientId} confirmado com sucesso.")]
    public static partial void EmailConfirmed(ILogger logger, int clientId);

    [LoggerMessage(EventId = 1504, Level = LogLevel.Warning, Message = "Link de confirmação expirado usado pelo cliente {ClientId}.")]
    public static partial void ExpiredConfirmationLinkUsed(ILogger logger, int clientId);

    [LoggerMessage(EventId = 1505, Level = LogLevel.Warning, Message = "Tentativa de confirmação de e-mail com link inválido.")]
    public static partial void InvalidConfirmationLinkUsed(ILogger logger);

    [LoggerMessage(EventId = 1506, Level = LogLevel.Warning, Message = "Login recusado para o cliente {ClientId}: e-mail ainda não confirmado.")]
    public static partial void LoginRejectedEmailNotConfirmed(ILogger logger, int clientId);

    [LoggerMessage(EventId = 1507, Level = LogLevel.Information, Message = "Novo link de confirmação emitido para o cliente {ClientId}.")]
    public static partial void NewConfirmationLinkIssued(ILogger logger, int clientId);

    [LoggerMessage(EventId = 1508, Level = LogLevel.Warning, Message = "Pedido de novo link de confirmação do cliente {ClientId} recusado por limite de emissão.")]
    public static partial void NewConfirmationLinkThrottled(ILogger logger, int clientId);

    [LoggerMessage(EventId = 1509, Level = LogLevel.Error, Message = "Fila de e-mails de confirmação cheia: mensagem do cliente {ClientId} descartada.")]
    public static partial void ConfirmationEmailQueueRejected(ILogger logger, int clientId);
}

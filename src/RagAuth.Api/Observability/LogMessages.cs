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
}

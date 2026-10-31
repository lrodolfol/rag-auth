namespace RagAuth.Api.Domain.Exceptions;

public sealed class RagApiTimeoutException()
    : DomainException(
        StatusCodes.Status504GatewayTimeout,
        "Tempo esgotado",
        "O serviço de recursos demorou demais para responder.");

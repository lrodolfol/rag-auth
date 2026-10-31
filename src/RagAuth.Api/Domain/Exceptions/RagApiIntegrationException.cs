namespace RagAuth.Api.Domain.Exceptions;

public sealed class RagApiIntegrationException()
    : DomainException(
        StatusCodes.Status502BadGateway,
        "Falha de integração",
        "Falha na comunicação com o serviço de recursos. Tente novamente mais tarde.");

namespace RagAuth.Api.Domain.Exceptions;

public sealed class RagApiUnavailableException()
    : DomainException(
        StatusCodes.Status503ServiceUnavailable,
        "Serviço indisponível",
        "O serviço de recursos está indisponível no momento.");

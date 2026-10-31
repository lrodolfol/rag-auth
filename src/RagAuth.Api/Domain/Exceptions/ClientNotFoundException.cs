namespace RagAuth.Api.Domain.Exceptions;

public sealed class ClientNotFoundException()
    : DomainException(StatusCodes.Status401Unauthorized, "Não autenticado", "Token ausente, inválido ou expirado.");

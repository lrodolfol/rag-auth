namespace RagAuth.Api.Domain.Exceptions;

public sealed class InvalidCredentialsException()
    : DomainException(StatusCodes.Status401Unauthorized, "Não autenticado", "E-mail ou senha inválidos.");

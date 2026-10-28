namespace RagAuth.Api.Domain.Exceptions;

public sealed class EmailAlreadyRegisteredException()
    : DomainException(StatusCodes.Status409Conflict, "Conflito", "Já existe uma conta cadastrada com este e-mail.");

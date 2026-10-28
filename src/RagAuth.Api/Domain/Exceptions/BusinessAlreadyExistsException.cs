namespace RagAuth.Api.Domain.Exceptions;

public sealed class BusinessAlreadyExistsException()
    : DomainException(StatusCodes.Status409Conflict, "Conflito", "Você já possui um negócio cadastrado.");

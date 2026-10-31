namespace RagAuth.Api.Domain.Exceptions;

public sealed class BusinessNotFoundException()
    : DomainException(StatusCodes.Status404NotFound, "Recurso não encontrado", "Não há negócio cadastrado para este cliente.");

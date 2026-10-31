namespace RagAuth.Api.Domain.Exceptions;

public sealed class AccessCodeGenerationException()
    : DomainException(StatusCodes.Status500InternalServerError, "Erro interno", "Não foi possível concluir o cadastro. Tente novamente.");

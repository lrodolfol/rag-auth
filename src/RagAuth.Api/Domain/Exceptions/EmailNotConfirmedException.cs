namespace RagAuth.Api.Domain.Exceptions;

public sealed class EmailNotConfirmedException()
    : DomainException(
        StatusCodes.Status403Forbidden,
        "Acesso negado",
        "O e-mail ainda não foi confirmado. Confirme seu e-mail pelo link enviado ou solicite um novo link para acessar a aplicação.");

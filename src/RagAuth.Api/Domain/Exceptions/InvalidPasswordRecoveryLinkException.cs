namespace RagAuth.Api.Domain.Exceptions;

public sealed class InvalidPasswordRecoveryLinkException()
    : DomainException(
        StatusCodes.Status400BadRequest,
        "Requisição inválida",
        "Link de redefinição de senha inválido. Use o link do e-mail de recuperação mais recente.");

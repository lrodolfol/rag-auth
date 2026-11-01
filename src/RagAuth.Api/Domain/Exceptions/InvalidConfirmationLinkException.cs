namespace RagAuth.Api.Domain.Exceptions;

public sealed class InvalidConfirmationLinkException()
    : DomainException(
        StatusCodes.Status400BadRequest,
        "Requisição inválida",
        "Link de confirmação inválido. Use o link do e-mail de confirmação mais recente.");

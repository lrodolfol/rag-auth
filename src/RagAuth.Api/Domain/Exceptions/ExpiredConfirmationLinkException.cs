namespace RagAuth.Api.Domain.Exceptions;

public sealed class ExpiredConfirmationLinkException()
    : DomainException(
        StatusCodes.Status410Gone,
        "Link expirado",
        "O link de confirmação expirou. Solicite um novo link de confirmação.");

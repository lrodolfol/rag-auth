namespace RagAuth.Api.Domain.Exceptions;

public sealed class ExpiredPasswordRecoveryLinkException()
    : DomainException(
        StatusCodes.Status410Gone,
        "Link expirado",
        "O link de redefinição de senha expirou. Solicite uma nova recuperação de senha.");

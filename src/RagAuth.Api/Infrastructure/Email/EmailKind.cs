namespace RagAuth.Api.Infrastructure.Email;

/// <summary>Tipo do e-mail transacional: escolhe o modelo e o assunto (research R11).</summary>
public enum EmailKind
{
    EmailConfirmation,
    PasswordRecovery,
    PasswordChanged,
}

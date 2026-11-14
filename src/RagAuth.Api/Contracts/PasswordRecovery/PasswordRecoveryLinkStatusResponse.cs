namespace RagAuth.Api.Contracts.PasswordRecovery;

/// <summary>Resultado da verificação do link. Nunca traz nome, e-mail nem identificador do cliente.</summary>
public sealed record PasswordRecoveryLinkStatusResponse(string Status)
{
    public const string ValidStatus = "valid";
}

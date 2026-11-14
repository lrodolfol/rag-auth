namespace RagAuth.Api.Services.PasswordRecovery;

public interface IPasswordRecoveryService
{
    /// <summary>Emite o link quando cabível; nunca revela, por exceção, retorno ou tempo, a situação da conta.</summary>
    Task RequestRecoveryAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Confere o link sem consumi-lo; lança exceção de domínio se ele for inválido ou estiver expirado.</summary>
    Task VerifyLinkAsync(string selector, string validator, CancellationToken cancellationToken = default);

    /// <summary>Troca a senha e consome o link numa única transação; não emite token.</summary>
    Task ResetPasswordAsync(string selector, string validator, string newPassword, CancellationToken cancellationToken = default);
}

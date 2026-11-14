using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "email";

    public static readonly int[] DefaultRetryDelaysSeconds = [30, 120];

    [Required(ErrorMessage = "A configuração 'email:fromAddress' é obrigatória.")]
    [EmailAddress(ErrorMessage = "A configuração 'email:fromAddress' deve ser um e-mail válido.")]
    public string FromAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "A configuração 'email:appName' é obrigatória.")]
    public string AppName { get; set; } = "RagAuth";

    [Required(ErrorMessage = "A configuração 'email:confirmationPageBaseUrl' é obrigatória.")]
    [Url(ErrorMessage = "A configuração 'email:confirmationPageBaseUrl' deve ser uma URL absoluta.")]
    public string ConfirmationPageBaseUrl { get; set; } = "https://ragauth.com.br";

    [Range(0, 1440, ErrorMessage = "A configuração 'email:newLinkIntervalMinutes' deve estar entre 0 e 1440.")]
    public int NewLinkIntervalMinutes { get; set; } = 5;

    [Range(1, 100, ErrorMessage = "A configuração 'email:maximumNewLinksPerDay' deve estar entre 1 e 100.")]
    public int MaximumNewLinksPerDay { get; set; } = 10;

    [Required(ErrorMessage = "A configuração 'email:passwordRecoveryPageBaseUrl' é obrigatória.")]
    [Url(ErrorMessage = "A configuração 'email:passwordRecoveryPageBaseUrl' deve ser uma URL absoluta.")]
    public string PasswordRecoveryPageBaseUrl { get; set; } = "https://ragauth.com.br";

    [Range(0, 1440, ErrorMessage = "A configuração 'email:passwordRecoveryIntervalMinutes' deve estar entre 0 e 1440.")]
    public int PasswordRecoveryIntervalMinutes { get; set; } = 5;

    [Range(1, 100, ErrorMessage = "A configuração 'email:maximumPasswordRecoveryLinksPerDay' deve estar entre 1 e 100.")]
    public int MaximumPasswordRecoveryLinksPerDay { get; set; } = 10;

    /// <summary>Duração mínima do pedido de recuperação, para não revelar pelo tempo se a conta existe (SC-003).</summary>
    [Range(0, 5000, ErrorMessage = "A configuração 'email:passwordRecoveryMinimumResponseMilliseconds' deve estar entre 0 e 5000.")]
    public int PasswordRecoveryMinimumResponseMilliseconds { get; set; } = 300;

    /// <remarks>
    /// Começa vazio porque o binder de configuração acrescenta itens a arrays já preenchidos;
    /// o padrão <see cref="DefaultRetryDelaysSeconds"/> é aplicado no PostConfigure.
    /// </remarks>
    public int[] RetryDelaysSeconds { get; set; } = [];

    [Required(ErrorMessage = "A variável de ambiente RESEND_API_KEY é obrigatória.")]
    public string ResendApiKey { get; set; } = string.Empty;
}

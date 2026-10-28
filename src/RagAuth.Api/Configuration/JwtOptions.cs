using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "jwt";

    [Required(ErrorMessage = "A configuração 'jwt:issuer' é obrigatória.")]
    public string Issuer { get; set; } = string.Empty;

    [Required(ErrorMessage = "A configuração 'jwt:audience' é obrigatória.")]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "A configuração 'jwt:expirationMinutes' deve estar entre 1 e 1440.")]
    public int ExpirationMinutes { get; set; } = 60;

    [Required(ErrorMessage = "A variável de ambiente JWT_SIGNING_KEY é obrigatória.")]
    [MinLength(32, ErrorMessage = "A variável de ambiente JWT_SIGNING_KEY deve ter no mínimo 32 caracteres.")]
    public string SigningKey { get; set; } = string.Empty;
}

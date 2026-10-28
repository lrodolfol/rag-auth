using System.ComponentModel.DataAnnotations;
using RagAuth.Api.Contracts.Validation;

namespace RagAuth.Api.Contracts.Auth;

public sealed class RegisterClientRequest
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(150, ErrorMessage = "O nome deve ter no máximo 150 caracteres.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [MaxLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [MinLength(8, ErrorMessage = AlphanumericPasswordAttribute.DefaultErrorMessage)]
    [MaxLength(128, ErrorMessage = "A senha deve ter no máximo 128 caracteres.")]
    [AlphanumericPassword]
    public string Password { get; init; } = string.Empty;
}

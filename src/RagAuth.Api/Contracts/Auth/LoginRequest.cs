using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Contracts.Auth;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    public string Password { get; init; } = string.Empty;
}

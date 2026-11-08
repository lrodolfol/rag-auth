using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Contracts.EmailConfirmation;

public sealed class NewConfirmationLinkRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [MaxLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string Email { get; init; } = string.Empty;
}

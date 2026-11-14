using System.ComponentModel.DataAnnotations;
using RagAuth.Api.Contracts.Validation;

namespace RagAuth.Api.Contracts.PasswordRecovery;

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = "Informe a nova senha.")]
    [MinLength(8, ErrorMessage = AlphanumericPasswordAttribute.DefaultErrorMessage)]
    [MaxLength(128, ErrorMessage = "A senha deve ter no máximo 128 caracteres.")]
    [AlphanumericPassword]
    public string NewPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a confirmação da nova senha.")]
    [Compare(nameof(NewPassword), ErrorMessage = "A confirmação da senha deve ser igual à nova senha.")]
    public string NewPasswordConfirmation { get; init; } = string.Empty;
}

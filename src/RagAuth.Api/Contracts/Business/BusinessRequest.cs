using System.ComponentModel.DataAnnotations;
using RagAuth.Api.Contracts.Validation;

namespace RagAuth.Api.Contracts.Business;

public sealed class BusinessRequest
{
    [Required(ErrorMessage = "Informe o nome do negócio.")]
    [MaxLength(150, ErrorMessage = "O nome do negócio deve ter no máximo 150 caracteres.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a descrição do negócio.")]
    [MaxLength(1000, ErrorMessage = "A descrição deve ter no máximo 1.000 caracteres.")]
    public string Description { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [MaxLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string? Email { get; init; }

    [BrazilianPhone]
    public string? Telephone { get; init; }

    [MaxLength(300, ErrorMessage = "O endereço deve ter no máximo 300 caracteres.")]
    [SingleLine]
    public string? Address { get; init; }

    [MaxLength(2000, ErrorMessage = "Os detalhes devem ter no máximo 2.000 caracteres.")]
    public string? Details { get; init; }
}

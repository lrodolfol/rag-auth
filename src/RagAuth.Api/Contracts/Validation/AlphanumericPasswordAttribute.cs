using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Contracts.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class AlphanumericPasswordAttribute() : ValidationAttribute(DefaultErrorMessage)
{
    public const string DefaultErrorMessage = "A senha deve ter no mínimo 8 caracteres, com ao menos uma letra e um número.";

    public override bool IsValid(object? value) =>
        value is null || (value is string password && password.Any(char.IsLetter) && password.Any(char.IsDigit));
}

using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Contracts.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class BrazilianPhoneAttribute() : ValidationAttribute(DefaultErrorMessage)
{
    public const string DefaultErrorMessage = "Informe um telefone com DDD, com 10 ou 11 dígitos.";

    private static readonly char[] AllowedMaskCharacters = [' ', '(', ')', '-', '+', '.'];

    public override bool IsValid(object? value)
    {
        if (value is null || (value is string emptyText && string.IsNullOrWhiteSpace(emptyText)))
        {
            return true;
        }

        return value is string telephone && HasOnlyDigitsAndMask(telephone) && HasValidDigitCount(telephone);
    }

    private static bool HasOnlyDigitsAndMask(string telephone) =>
        telephone.All(character => char.IsAsciiDigit(character) || AllowedMaskCharacters.Contains(character));

    private static bool HasValidDigitCount(string telephone)
    {
        var digitCount = telephone.Count(char.IsAsciiDigit);
        return digitCount is 10 or 11;
    }
}

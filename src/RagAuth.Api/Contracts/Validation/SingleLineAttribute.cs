using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Contracts.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SingleLineAttribute() : ValidationAttribute(DefaultErrorMessage)
{
    public const string DefaultErrorMessage = "O endereço deve ser informado em uma única linha.";

    public override bool IsValid(object? value) =>
        value is not string text || text.IndexOfAny(['\r', '\n']) < 0;
}

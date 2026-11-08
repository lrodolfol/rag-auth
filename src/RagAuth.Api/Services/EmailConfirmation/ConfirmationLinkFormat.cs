namespace RagAuth.Api.Services.EmailConfirmation;

/// <summary>
/// Formato do link é regra de domínio (research R3): selector com 22 e validator com 43 caracteres Base64Url.
/// </summary>
public static class ConfirmationLinkFormat
{
    public static bool IsValid(string? selector, string? validator) =>
        IsBase64UrlWithLength(selector, RandomConfirmationLinkGenerator.SelectorLength)
        && IsBase64UrlWithLength(validator, RandomConfirmationLinkGenerator.ValidatorLength);

    private static bool IsBase64UrlWithLength(string? value, int expectedLength) =>
        value is not null
        && value.Length == expectedLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}

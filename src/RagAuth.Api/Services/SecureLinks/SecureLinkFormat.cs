namespace RagAuth.Api.Services.SecureLinks;

/// <summary>
/// Formato do link é regra de domínio: selector com 22 e validator com 43 caracteres Base64Url.
/// </summary>
public static class SecureLinkFormat
{
    public static bool IsValid(string? selector, string? validator) =>
        IsBase64UrlWithLength(selector, RandomSecureLinkGenerator.SelectorLength)
        && IsBase64UrlWithLength(validator, RandomSecureLinkGenerator.ValidatorLength);

    private static bool IsBase64UrlWithLength(string? value, int expectedLength) =>
        value is not null
        && value.Length == expectedLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}

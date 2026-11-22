using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Configuration;

public sealed class FrontendCorsOptions : IValidatableObject
{
    public const string SectionName = "cors";

    private const string EmptyOriginsMessage = "A configuração 'cors:allowedOrigins' deve conter ao menos uma origem.";
    private const string NotAbsoluteUrlMessage = "A configuração 'cors:allowedOrigins' deve conter apenas URLs absolutas.";

    public string[] AllowedOrigins { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        string[] memberNames = [nameof(AllowedOrigins)];
        if (AllowedOrigins.Length == 0)
        {
            yield return new ValidationResult(EmptyOriginsMessage, memberNames);
        }
        else if (!AllowedOrigins.All(IsAbsoluteHttpUrl))
        {
            yield return new ValidationResult(NotAbsoluteUrlMessage, memberNames);
        }
    }

    private static bool IsAbsoluteHttpUrl(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
        && (originUri.Scheme == Uri.UriSchemeHttp || originUri.Scheme == Uri.UriSchemeHttps);
}

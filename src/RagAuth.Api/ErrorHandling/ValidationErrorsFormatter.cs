using System.Text.Json;

namespace RagAuth.Api.ErrorHandling;

/// <summary>
/// A validação nativa usa o nome da propriedade C# como chave; o contrato da API usa camelCase.
/// </summary>
public static class ValidationErrorsFormatter
{
    public static void FormatErrors(HttpValidationProblemDetails validationProblemDetails)
    {
        var formattedErrors = validationProblemDetails.Errors.ToDictionary(
            error => ToCamelCasePath(error.Key),
            error => error.Value.Distinct().ToArray());
        validationProblemDetails.Errors.Clear();
        foreach (var formattedError in formattedErrors)
        {
            validationProblemDetails.Errors[formattedError.Key] = formattedError.Value;
        }
    }

    private static string ToCamelCasePath(string fieldPath) =>
        string.Join('.', fieldPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}

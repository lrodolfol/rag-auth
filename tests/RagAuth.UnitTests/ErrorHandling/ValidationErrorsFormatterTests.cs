using Microsoft.AspNetCore.Http;
using RagAuth.Api.ErrorHandling;

namespace RagAuth.UnitTests.ErrorHandling;

public sealed class ValidationErrorsFormatterTests
{
    [Fact]
    public void ConvertsFieldPathsToCamelCaseAndRemovesRepeatedMessages()
    {
        var problemDetails = new HttpValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["Password"] = ["Senha fraca.", "Senha fraca."],
            ["Request.Name"] = ["Informe o nome."],
        });

        ValidationErrorsFormatter.FormatErrors(problemDetails);

        Assert.Equal(["password", "request.name"], problemDetails.Errors.Keys.Order());
        Assert.Equal(["Senha fraca."], problemDetails.Errors["password"]);
        Assert.Equal(["Informe o nome."], problemDetails.Errors["request.name"]);
    }
}

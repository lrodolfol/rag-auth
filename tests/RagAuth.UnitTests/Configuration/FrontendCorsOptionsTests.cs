using System.ComponentModel.DataAnnotations;
using RagAuth.Api.Configuration;

namespace RagAuth.UnitTests.Configuration;

public sealed class FrontendCorsOptionsTests
{
    private const string EmptyOriginsMessage = "A configuração 'cors:allowedOrigins' deve conter ao menos uma origem.";
    private const string NotAbsoluteUrlMessage = "A configuração 'cors:allowedOrigins' deve conter apenas URLs absolutas.";

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("http://localhost:4200/")]
    [InlineData("http://127.0.0.1:4200")]
    [InlineData("http://127.0.0.1:4200/")]
    public void OptionsWithAbsoluteHttpOriginAreValid(string origin)
    {
        var validationResults = Validate(new FrontendCorsOptions { AllowedOrigins = [origin] });

        Assert.Empty(validationResults);
    }

    [Fact]
    public void OptionsWithoutOriginsAreInvalid()
    {
        var validationResults = Validate(new FrontendCorsOptions { AllowedOrigins = [] });

        var validationResult = Assert.Single(validationResults);
        Assert.Equal(EmptyOriginsMessage, validationResult.ErrorMessage);
        Assert.Equal([nameof(FrontendCorsOptions.AllowedOrigins)], validationResult.MemberNames);
    }

    [Theory]
    [InlineData("nao-e-uma-url")]
    [InlineData("localhost:4200")]
    [InlineData("ftp://localhost:4200")]
    [InlineData("")]
    public void OptionsWithOriginThatIsNotAbsoluteHttpUrlAreInvalid(string origin)
    {
        var validationResults = Validate(new FrontendCorsOptions { AllowedOrigins = ["http://localhost:4200", origin] });

        var validationResult = Assert.Single(validationResults);
        Assert.Equal(NotAbsoluteUrlMessage, validationResult.ErrorMessage);
    }

    private static List<ValidationResult> Validate(FrontendCorsOptions options)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), validationResults, validateAllProperties: true);
        return validationResults;
    }
}

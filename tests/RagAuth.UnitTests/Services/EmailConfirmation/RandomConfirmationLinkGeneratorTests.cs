using RagAuth.Api.Services.EmailConfirmation;

namespace RagAuth.UnitTests.Services.EmailConfirmation;

public sealed class RandomConfirmationLinkGeneratorTests
{
    private readonly RandomConfirmationLinkGenerator _generator = new();

    [Fact]
    public void SelectorAndValidatorHaveExpectedLengthsAndUseBase64UrlAlphabet()
    {
        var selector = _generator.GenerateSelector();
        var validator = _generator.GenerateValidator();

        Assert.Equal(22, selector.Length);
        Assert.Equal(43, validator.Length);
        Assert.True(ConfirmationLinkFormat.IsValid(selector, validator));
    }

    [Fact]
    public void GeneratedValuesAreDifferentEachTime()
    {
        Assert.NotEqual(_generator.GenerateSelector(), _generator.GenerateSelector());
        Assert.NotEqual(_generator.GenerateValidator(), _generator.GenerateValidator());
    }

    [Fact]
    public void ValidatorHashIsDeterministicLowercaseHexWithSixtyFourCharacters()
    {
        var hash = _generator.ComputeValidatorHash("validator", "Ab3dEf6hIj9k");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, _generator.ComputeValidatorHash("validator", "Ab3dEf6hIj9k"));
        Assert.All(hash, character => Assert.True(char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'));
    }

    [Fact]
    public void ValidatorHashChangesWithAccessCodeOrValidator()
    {
        var hash = _generator.ComputeValidatorHash("validator", "Ab3dEf6hIj9k");

        Assert.NotEqual(hash, _generator.ComputeValidatorHash("validator", "OutroCodigo1"));
        Assert.NotEqual(hash, _generator.ComputeValidatorHash("outro-validator", "Ab3dEf6hIj9k"));
    }

    [Fact]
    public void ValidatorMatchRequiresSameValidatorAndAccessCode()
    {
        var validator = _generator.GenerateValidator();
        var storedHash = _generator.ComputeValidatorHash(validator, "Ab3dEf6hIj9k");

        Assert.True(_generator.IsValidatorMatch(validator, "Ab3dEf6hIj9k", storedHash));
        Assert.False(_generator.IsValidatorMatch(validator, "OutroCodigo1", storedHash));
        Assert.False(_generator.IsValidatorMatch(_generator.GenerateValidator(), "Ab3dEf6hIj9k", storedHash));
        Assert.False(_generator.IsValidatorMatch(validator, "Ab3dEf6hIj9k", "curto"));
    }

    [Theory]
    [InlineData(null, "VALIDATOR")]
    [InlineData("curto", "VALIDATOR")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA+", "VALIDATOR")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA", "curto")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA", null)]
    public void FormatRejectsWrongLengthsOrCharacters(string? selector, string? validator)
    {
        var resolvedValidator = validator == "VALIDATOR" ? new string('B', 43) : validator;

        Assert.False(ConfirmationLinkFormat.IsValid(selector, resolvedValidator));
    }
}

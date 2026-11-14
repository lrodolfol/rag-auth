using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Services.SecureLinks;

namespace RagAuth.UnitTests.Services.SecureLinks;

public sealed class RandomSecureLinkGeneratorTests
{
    private const string ConfirmationPurpose = EmailConfirmationLink.EmailConfirmationPurpose;
    private const string RecoveryPurpose = PasswordRecoveryLink.PasswordRecoveryPurpose;

    // SHA-256("validator:Ab3dEf6hIj9k:email-confirmation"), calculado antes da generalização do gerador.
    private const string ConfirmationHashBeforeRefactoring = "9871fdf592e9e5ff9b762387db9c3216415617a7586f44f8fac8aa1e494d77b4";

    private readonly RandomSecureLinkGenerator _generator = new();

    [Fact]
    public void SelectorAndValidatorHaveExpectedLengthsAndUseBase64UrlAlphabet()
    {
        var selector = _generator.GenerateSelector();
        var validator = _generator.GenerateValidator();

        Assert.Equal(22, selector.Length);
        Assert.Equal(43, validator.Length);
        Assert.True(SecureLinkFormat.IsValid(selector, validator));
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
        var hash = _generator.ComputeValidatorHash("validator", "Ab3dEf6hIj9k", ConfirmationPurpose);

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, _generator.ComputeValidatorHash("validator", "Ab3dEf6hIj9k", ConfirmationPurpose));
        Assert.All(hash, character => Assert.True(char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'));
    }

    [Fact]
    public void ValidatorHashChangesWithAccessCodeOrValidator()
    {
        var hash = _generator.ComputeValidatorHash("validator", "Ab3dEf6hIj9k", ConfirmationPurpose);

        Assert.NotEqual(hash, _generator.ComputeValidatorHash("validator", "OutroCodigo1", ConfirmationPurpose));
        Assert.NotEqual(hash, _generator.ComputeValidatorHash("outro-validator", "Ab3dEf6hIj9k", ConfirmationPurpose));
    }

    [Fact]
    public void ValidatorMatchRequiresSameValidatorAndAccessCode()
    {
        var validator = _generator.GenerateValidator();
        var storedHash = _generator.ComputeValidatorHash(validator, "Ab3dEf6hIj9k", ConfirmationPurpose);

        Assert.True(_generator.IsValidatorMatch(validator, "Ab3dEf6hIj9k", ConfirmationPurpose, storedHash));
        Assert.False(_generator.IsValidatorMatch(validator, "OutroCodigo1", ConfirmationPurpose, storedHash));
        Assert.False(_generator.IsValidatorMatch(_generator.GenerateValidator(), "Ab3dEf6hIj9k", ConfirmationPurpose, storedHash));
        Assert.False(_generator.IsValidatorMatch(validator, "Ab3dEf6hIj9k", ConfirmationPurpose, "curto"));
    }

    [Fact]
    public void ValidatorHashDiffersBetweenPurposes()
    {
        var confirmationHash = _generator.ComputeValidatorHash("validator", "Ab3dEf6hIj9k", ConfirmationPurpose);
        var recoveryHash = _generator.ComputeValidatorHash("validator", "Ab3dEf6hIj9k", RecoveryPurpose);

        Assert.NotEqual(confirmationHash, recoveryHash);
    }

    [Fact]
    public void ValidatorMatchWithWrongPurposeReturnsFalse()
    {
        var validator = _generator.GenerateValidator();
        var confirmationHash = _generator.ComputeValidatorHash(validator, "Ab3dEf6hIj9k", ConfirmationPurpose);

        Assert.False(_generator.IsValidatorMatch(validator, "Ab3dEf6hIj9k", RecoveryPurpose, confirmationHash));
    }

    [Fact]
    public void ConfirmationHashIsIdenticalToTheOneComputedBeforeRefactoring()
    {
        var hash = _generator.ComputeValidatorHash("validator", "Ab3dEf6hIj9k", ConfirmationPurpose);

        Assert.Equal(ConfirmationHashBeforeRefactoring, hash);
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

        Assert.False(SecureLinkFormat.IsValid(selector, resolvedValidator));
    }
}

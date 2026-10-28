using RagAuth.Api.Contracts.Validation;

namespace RagAuth.UnitTests.Contracts.Validation;

public sealed class BrazilianPhoneAttributeTests
{
    private readonly BrazilianPhoneAttribute _attribute = new();

    [Theory]
    [InlineData("(11) 98765-4321")]
    [InlineData("1132654321")]
    [InlineData(null)]
    [InlineData("   ")]
    public void AcceptsPhoneWithAreaCodeOrMissingValue(string? telephone)
    {
        Assert.True(_attribute.IsValid(telephone));
    }

    [Theory]
    [InlineData("987654321")]
    [InlineData("119876543210")]
    [InlineData("11 9876-ABCD")]
    public void RejectsWrongDigitCountOrLetters(string telephone)
    {
        Assert.False(_attribute.IsValid(telephone));
    }

    [Fact]
    public void UsesPortugueseErrorMessage()
    {
        Assert.Equal("Informe um telefone com DDD, com 10 ou 11 dígitos.", _attribute.FormatErrorMessage("telephone"));
    }
}

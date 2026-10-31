using RagAuth.Api.Contracts.Validation;

namespace RagAuth.UnitTests.Contracts.Validation;

public sealed class AlphanumericPasswordAttributeTests
{
    private readonly AlphanumericPasswordAttribute _attribute = new();

    [Fact]
    public void AcceptsPasswordWithLettersAndDigits()
    {
        Assert.True(_attribute.IsValid("senhaSegura1"));
    }

    [Theory]
    [InlineData("somenteletras")]
    [InlineData("12345678")]
    [InlineData("")]
    public void RejectsPasswordWithoutLetterAndDigit(string password)
    {
        Assert.False(_attribute.IsValid(password));
    }

    [Fact]
    public void LeavesMissingValueToRequiredAttribute()
    {
        Assert.True(_attribute.IsValid(null));
    }

    [Fact]
    public void UsesPortugueseErrorMessage()
    {
        Assert.Equal(
            "A senha deve ter no mínimo 8 caracteres, com ao menos uma letra e um número.",
            _attribute.FormatErrorMessage("password"));
    }
}
